namespace WebApplication1.Models
{
    public class FileCacheEntry<T>
    {
        public T? Data { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}