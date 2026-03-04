using System;
using System.Net;

namespace WallpaperDestop.Services
{
    /// <summary>
    /// Custom exception for Unsplash API rate limit errors (HTTP 403/429)
    /// </summary>
    public class UnsplashRateLimitException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        
        public UnsplashRateLimitException(HttpStatusCode statusCode) 
            : base("Đã hết lượt tải ảnh miễn phí. Vui lòng thử lại sau")
        {
            StatusCode = statusCode;
        }
        
        public UnsplashRateLimitException(HttpStatusCode statusCode, string message) 
            : base(message)
        {
            StatusCode = statusCode;
        }
        
        public UnsplashRateLimitException(HttpStatusCode statusCode, string message, Exception innerException) 
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }
}