namespace ValorantApp.GenericExtensions
{
    public static class CollectionsExtension
    {
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> collection) 
        {
            if (collection == null || collection.Count() == 0)
            {
                return true;
            }
            return false;
        }

        public static IEnumerable<T> DequeueCount<T>(this Queue<T> queue, int count)
        {
            int countMin = Math.Min(queue.Count, count);
            for (int i = 0; i < countMin; i++)
            {
                yield return queue.Dequeue();
            }
        }
    }
}
