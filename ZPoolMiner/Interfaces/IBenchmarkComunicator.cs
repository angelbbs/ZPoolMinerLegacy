namespace ZPoolMiner.Interfaces
{
    public interface IBenchmarkComunicator
    {
        void OnBenchmarkComplete(bool success, string status);
    }
}
