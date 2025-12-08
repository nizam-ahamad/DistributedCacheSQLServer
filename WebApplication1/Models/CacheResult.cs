namespace WebApplication1.Models
{
    public class CacheResult<T> where T : class
    {
        public T? Data { get; set; }
        public string Source { get; set; } = "none";
        public bool IsFromCache { get; set; }
    }
}