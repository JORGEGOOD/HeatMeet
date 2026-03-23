namespace SharedModels
{
    public class NetworkMessage
    {
        public string Command { get; set; } = string.Empty;
        public object? Data { get; set; }
    }
}
