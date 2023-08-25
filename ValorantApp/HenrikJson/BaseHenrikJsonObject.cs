namespace ValorantApp
{
    public class JsonObjectHenrik<T>
    {
        public string Status { get; set; }
        public T? Data { get; set; }
        public ErrorsJson? Errors { get; set; }
    }

    public class ErrorsJson
    {
        public string Message { get; set; }
        public int code { get; set; }
        public string Details { get; set; }
    }
}