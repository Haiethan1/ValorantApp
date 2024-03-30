namespace ValorantApp.GenericExtensions
{
    public static class HttpClientExtensions
    {
        public static long GetRequestSize(HttpRequestMessage request)
        {
            // Calculate the size of the request (headers + content length)
            var headersSize = request.Headers.Sum(header => header.Key.Length + header.Value.Sum(value => value.Length));
            var contentSize = request.Content?.Headers?.ContentLength ?? 0;
            return headersSize + contentSize;
        }

        public static async Task<long> GetResponseSize(this HttpResponseMessage response)
        {
            // Calculate the size of the response (headers + content length)
            var headersSize = response.Headers.Sum(header => header.Key.Length + header.Value.Sum(value => value.Length));
            var contentSize = response.Content.Headers.ContentLength ?? (await response.Content.ReadAsByteArrayAsync()).Length;
            return headersSize + contentSize;
        }

        public static string FormatSize(this long size)
        {
            const long kb = 1024;
            const long mb = kb * 1024;
            const long gb = mb * 1024;

            if (size >= gb)
            {
                return $"{(double)size / gb:N2} GB";
            }
            else if (size >= mb)
            {
                return $"{(double)size / mb:N2} MB";
            }
            else if (size >= kb)
            {
                return $"{(double)size / kb:N2} KB";
            }
            else
            {
                return $"{size} bytes";
            }
        }
    }
}
