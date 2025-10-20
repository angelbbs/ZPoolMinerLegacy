using Microsoft.Win32;
using Newtonsoft.Json;
using ZPoolMiner.Configs;
using ZPoolMiner.Forms;
using ZPoolMiner.Miners;
using ZPoolMiner.Stats;
using ZPoolMiner.Utils;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Permissions;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static ZPoolMiner.Wallets.Wallets;
using System.Collections.Generic;
using static ZPoolMiner.Stats.Stats;
using ZPoolMiner.Devices;

namespace ZPoolMiner
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        ///
        public class SplashForm : Form
        {
            private delegate void CloseDelegate();
            private static Form splashForm;
            private static bool show = false;

            static public void ShowSplashScreen()
            {
                if (splashForm != null) return;
                Splash_Tick();
            }

            public static void Splash_Tick()
            {
                if (!show)
                {
                    show = true;
                    splashForm = new Form_Splash();
                    splashForm.Show();
                }

                for (double i = 0; i <= 1; i = i + 0.05)
                {
                    splashForm.Opacity = i;
                    splashForm.Update();
                    Thread.Sleep(20);
                }
            }

            static public void _CloseForm()
            {
                if (show)
                {
                    for (double i = 1; i > 0; i = i - 0.05)
                    {
                        splashForm.Opacity = i;
                        splashForm.Update();
                        Thread.Sleep(10);
                    }
                    CloseForm();
                }
            }

            static public void CloseForm()
            {
                splashForm?.Invoke(new CloseDelegate(SplashForm.CloseFormInternal));
            }

            static private void CloseFormInternal()
            {
                if (splashForm != null)
                {
                    show = false;
                    splashForm.Close();
                    splashForm = null;
                };
            }
        }

        [STAThread]
        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        static void Main(string[] argv)
        {
            WindowsPrincipal pricipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            bool hasAdministrativeRight = pricipal.IsInRole(WindowsBuiltInRole.Administrator);
            //var proc = Process.GetCurrentProcess();

            if (hasAdministrativeRight == false)
            {
                Helpers.ConsolePrint("Main", "Try Administrator");
                ProcessStartInfo processInfo = new ProcessStartInfo();
                processInfo.Verb = "runas";
                processInfo.FileName = Application.ExecutablePath;
                try
                {
                    Process.Start(processInfo);
                }
                catch (Win32Exception e)
                {
                    Helpers.ConsolePrint("Error start as Administrator: ", e.ToString());
                }
                return;
                //proc.Kill();
            } else
            {
                //Helpers.ConsolePrint("Main", "Administrator");
            }
            string conf = "";
            try
            {
                conf = File.ReadAllText("configs\\General.json");
            }
            catch
            {
                conf = "\"ShowSplash\": true";
            }
            if (conf.Contains("\"ShowSplash\": true") || !conf.Contains("\"ShowSplash")) SplashForm.ShowSplashScreen();

            // Set working directory to exe
            var pathSet = false;
            var path = Path.GetDirectoryName(Application.ExecutablePath);
            if (path != null)
            {
                Environment.CurrentDirectory = path;
                pathSet = true;
            }

            // Add common folder to path for launched processes
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            pathVar += ";" + Path.Combine(Environment.CurrentDirectory, "common");
            Environment.SetEnvironmentVariable("PATH", pathVar);

            //System.Reflection.Assembly.Load("CustomTabControl");
            System.Reflection.Assembly.Load("IGCL");
            System.Reflection.Assembly.Load("MSIAfterburner.NET");
            System.Reflection.Assembly.Load("ZPoolMinerLegacy");
            System.Reflection.Assembly.Load("ZPoolMinerLegacy.Overclock");
            System.Reflection.Assembly.Load("ZPoolMinerLegacy.Extensions");
            System.Reflection.Assembly.Load("ZPoolMinerLegacy.UUID");
            System.Reflection.Assembly.Load("NvidiaGPUGetDataHost");

            Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            //Console.OutputEncoding = System.Text.Encoding.Unicode;
            // #0 set this first so data parsing will work correctly
            Globals.JsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                Culture = CultureInfo.InvariantCulture
            };

            bool BackupRestoreFile = false;
            if (Directory.Exists("backup"))
            {
                var dirInfo = new DirectoryInfo("backup");
                foreach (var file in dirInfo.GetFiles())
                {
                    if (file.Name.Contains("backup_") && file.Name.Contains(".zip"))
                    {
                        BackupRestoreFile = true;
                    }
                }
            }

            try
            {
                var WDHandle = new Process
                {
                    StartInfo =
                {
                    FileName = "taskkill.exe"
                }
                };
                WDHandle.StartInfo.Arguments = "/F /IM MinerLegacyForkFixMonitor.exe";
                WDHandle.StartInfo.UseShellExecute = false;
                WDHandle.StartInfo.CreateNoWindow = true;
                WDHandle.Start();
            }
            catch (Exception ex)
            {
                Helpers.ConsolePrint("WatchDog", ex.ToString());
            }


            // #1 first initialize config
            if (!ConfigManager.InitializeConfig() && BackupRestoreFile)
            {
                var dialogRes = Utils.MessageBoxEx.Show("Restore from backup?", "Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Question, 15000);
                if (dialogRes == System.Windows.Forms.DialogResult.Yes)
                {
                        var CMDconfigHandleOHM = new Process

                        {
                            StartInfo =
                            {
                                FileName = "sc.exe"
                            }
                        };

                        CMDconfigHandleOHM.StartInfo.Arguments = "stop winring0_1_2_0";
                        CMDconfigHandleOHM.StartInfo.UseShellExecute = false;
                        CMDconfigHandleOHM.StartInfo.CreateNoWindow = true;
                        CMDconfigHandleOHM.Start();

                     CMDconfigHandleOHM = new Process

                        {
                            StartInfo =
                            {
                                FileName = "sc.exe"
                            }
                        };

                        CMDconfigHandleOHM.StartInfo.Arguments = "stop R0ZPoolMinerLegacy";
                        CMDconfigHandleOHM.StartInfo.UseShellExecute = false;
                        CMDconfigHandleOHM.StartInfo.CreateNoWindow = true;
                        CMDconfigHandleOHM.Start();

                    CMDconfigHandleOHM = new Process

                    {
                        StartInfo =
                            {
                                FileName = "sc.exe"
                            }
                    };

                    CMDconfigHandleOHM.StartInfo.Arguments = "delete R0ZPoolMinerLegacy";
                    CMDconfigHandleOHM.StartInfo.UseShellExecute = false;
                    CMDconfigHandleOHM.StartInfo.CreateNoWindow = true;
                    CMDconfigHandleOHM.Start();

                    MinersManager.StopAllMiners();
                    System.Threading.Thread.Sleep(5000);
                    Process.Start("backup\\restore.cmd");
                }
            }

            //checking for incompatibilities
            if (ConfigManager.GeneralConfig.AllowMultipleInstances && ConfigManager.GeneralConfig.ProgramMonitoring)
            {
                ConfigManager.GeneralConfig.AllowMultipleInstances = false;
                //ConfigManager.GeneralConfigFileCommit();
            }
            // #2 check if multiple instances are allowed
            var startProgram = true;
            if (ConfigManager.GeneralConfig.AllowMultipleInstances == false)
            {
                try
                {
                    var current = Process.GetCurrentProcess();
                    foreach (var process in Process.GetProcessesByName(current.ProcessName))
                    {
                        if (process.Id != current.Id)
                        {
                            startProgram = false;
                        }
                    }
                }
                catch { }
            }

            if (startProgram)
            {
                if (ConfigManager.GeneralConfig.LogToFile)
                {
                    if (!Directory.Exists("logs")) Directory.CreateDirectory("logs");
                    Logger.ConfigureWithFile();
                }

                
                if (Configs.ConfigManager.GeneralConfig.ForkFixVersion < 1.0 ||
                    !ConfigManager.GeneralConfig.Platform.Equals("ZPool"))
                {
                    Helpers.ConsolePrint("MinerLegacy", "Previous version: " + Configs.ConfigManager.GeneralConfig.ForkFixVersion.ToString());
                    ConfigManager.GeneralConfig.Platform = "ZPool";
                    ConfigManager.GeneralConfig.ForkFixVersion = 1.0;
                    ConfigManager.GeneralConfig.ServiceLocation = 1;
                }

                if (Configs.ConfigManager.GeneralConfig.ForkFixVersion < 1.1 ||
                    !ConfigManager.GeneralConfig.Platform.Equals("ZPool"))
                {
                    Helpers.ConsolePrint("MinerLegacy", "Previous version: " + Configs.ConfigManager.GeneralConfig.ForkFixVersion.ToString());
                    ConfigManager.GeneralConfig.Platform = "ZPool";
                    ConfigManager.GeneralConfig.ForkFixVersion = 1.1;
                    try
                    {
                        if (Directory.Exists("miners"))
                        {
                            var dirInfo = new DirectoryInfo("miners");
                            foreach (var file in dirInfo.GetFiles())
                            {
                                if (file.Name.Equals("vc_redist.x64.exe")) continue;
                                if (!file.Name.Equals("Fork_Fix_" + 
                                    ConfigManager.GeneralConfig.ForkFixVersion.ToString() + 
                                    ".txt"))
                                {
                                    file.Delete();
                                }
                            }
                        }
                    } catch (Exception ex)
                    {

                    }
                }
                //удалять ForkFix??.txt предыдущей версии майнеров

                new StorePermission(PermissionState.Unrestricted) { Flags = StorePermissionFlags.AddToStore }.Assert();
                X509Certificate2 certificate = new X509Certificate2(Properties.Resources.rootCA, "", X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

                using (var storeCU = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {
                    storeCU.Open(OpenFlags.ReadWrite | OpenFlags.MaxAllowed);

                    foreach (X509Certificate2 cert in storeCU.Certificates)
                    {
                        if (!cert.IssuerName.Name.Contains("Angelbbs"))
                        {
                            storeCU.Add(certificate);
                            storeCU.Close();
                            break;
                        }
                    }
                    storeCU.Close();
                }
                using (var storeLM = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
                {
                    storeLM.Open(OpenFlags.ReadWrite | OpenFlags.MaxAllowed);

                    foreach (X509Certificate2 cert in storeLM.Certificates)
                    {
                        if (!cert.IssuerName.Name.Contains("Angelbbs"))
                        {
                            storeLM.Add(certificate);
                            storeLM.Close();
                            break;
                        }
                    }
                    storeLM.Close();
                }
                //check after install
                using (var store2 = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
                {
                    store2.Open(OpenFlags.ReadWrite | OpenFlags.MaxAllowed);

                    foreach (X509Certificate2 cert in store2.Certificates)
                    {
                        if (cert.IssuerName.Name.Contains("Angelbbs"))
                        {
                            Form_Main.CertInstalled = true;
                            break;
                        }
                    }
                    store2.Close();
                }

                var CMDconfigHandleWD = new Process
                {
                    StartInfo =
                {
                    FileName = "sc.exe"
                }
                };

                CMDconfigHandleWD.StartInfo.Arguments = "stop WinDivert1.4";
                CMDconfigHandleWD.StartInfo.UseShellExecute = false;
                CMDconfigHandleWD.StartInfo.CreateNoWindow = true;
                CMDconfigHandleWD.Start();
                Thread.Sleep(200);
                var CMDconfigHandleWD1 = new Process
                {
                    StartInfo =
                {
                    FileName = "sc.exe"
                }
                };

                CMDconfigHandleWD1.StartInfo.Arguments = "delete WinDivert1.4";
                CMDconfigHandleWD1.StartInfo.UseShellExecute = false;
                CMDconfigHandleWD1.StartInfo.CreateNoWindow = true;
                CMDconfigHandleWD1.Start();

                var version = Assembly.GetExecutingAssembly().GetName().Version;
                var buildDate = new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.Revision * 2);
                Helpers.ConsolePrint("Start", "Starting up ZPoolMiner Legacy Fork Fix: Build date " + buildDate);
                // init active display currency after config load
                ExchangeRateApi.ActiveDisplayCurrency = ConfigManager.GeneralConfig.DisplayCurrency;

                // #2 then parse args
                var commandLineArgs = new CommandLineParser(argv);

                if (!pathSet)
                {
                    Helpers.ConsolePrint("Start", "Path not set to executable");
                }

                var tosChecked = ConfigManager.GeneralConfig.agreedWithTOS == Globals.CurrentTosVer;
                // Init languages
                International.Initialize(ConfigManager.GeneralConfig.Language);

                if (!tosChecked || !ConfigManager.GeneralConfigIsFileExist() && !commandLineArgs.IsLang)
                {
                    Helpers.ConsolePrint("Start",
                        "No config file found. Running Miner Legacy Fork Fix for the first time. Choosing a default language.");
                    //Application.Run(new Form_ChooseLanguage(true));
                    SplashForm.CloseForm();
                    var l = new Form_ChooseLanguage(true);
                    l.ShowDialog();
                    // Init languages
                    International.Initialize(ConfigManager.GeneralConfig.Language);
                }

                if (commandLineArgs.IsLang)
                {
                    Helpers.ConsolePrint("Start", "Language is overwritten by command line parameter (-lang).");
                    International.Initialize(commandLineArgs.LangValue);
                    ConfigManager.GeneralConfig.Language = commandLineArgs.LangValue;
                }
                // check WMI
                if (Helpers.IsWmiEnabled())
                {
                    try
                    {
                        var formmain = new Form_Main();
                        formmain.Hide();
                        //SplashForm.CloseForm();
                        Application.Run(formmain);
                    }
                    catch (Exception e)
                    {
                        Helpers.ConsolePrint("check WMI", e.Message);
                    }

                } else
                {
                    MessageBox.Show(International.GetText("Program_WMI_Error_Text"),
                        International.GetText("Program_WMI_Error_Title"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
