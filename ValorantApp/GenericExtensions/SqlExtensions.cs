using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics;

namespace ValorantApp.GenericExtensions
{
    public static class SqlExtensions
    {
        //private static readonly string connectionString = ConfigurationManager.ConnectionStrings[Debugger.IsAttached ? "DatabaseDev" : "DatabaseProd"].ConnectionString;
        //private static List<(string query, Dictionary<string, object> parameters, long executionTimeMs, byte retryCount, bool success, long timestamp)> queryLogBuffer = new();

        /// <summary>
        /// Adds a parameter to the SqlCommand with the specified name, SQL Server type, and value.
        /// </summary>
        /// <param name="command">The SqlCommand to add the parameter to.</param>
        /// <param name="name">The name of the parameter (e.g., @ColumnName).</param>
        /// <param name="dbType">The SQL Server type of the parameter (e.g., SqlDbType.Int).</param>
        /// <param name="value">The value to assign to the parameter.</param>
        /// <param name="size">Optional size for variable-length types like VarChar or NVarChar.</param>
        public static void AddParameter(this SqlCommand command, string name, SqlDbType dbType, object? value, int? size = null)
        {
            value = ConvertToSqlType(value, dbType);

            SqlParameter parameter = new(name, dbType) { Value = value ?? DBNull.Value };

            if (size.HasValue)
            {
                parameter.Size = size.Value;
            }

            command.Parameters.Add(parameter);
        }

        private static object ConvertToSqlType(object? value, SqlDbType dbType)
        {
            if (value == null) return null;

            try
            {
                return dbType switch
                {
                    SqlDbType.TinyInt => Convert.ToByte(value),    // Convert to byte for TinyInt
                    SqlDbType.SmallInt => Convert.ToInt16(value), // Convert to short for SmallInt
                    SqlDbType.Int => Convert.ToInt32(value),      // Convert to int for Int
                    SqlDbType.BigInt => Convert.ToInt64(value),   // Convert to long for BigInt
                    SqlDbType.Bit => Convert.ToBoolean(value),    // Convert to bool for Bit
                    SqlDbType.Decimal => Convert.ToDecimal(value),// Convert to decimal for Decimal
                    SqlDbType.Float => Convert.ToDouble(value),   // Convert to double for Float
                    SqlDbType.VarChar or SqlDbType.NVarChar => value.ToString(), // Convert to string for VarChar
                                                                                 // Add more conversions as needed
                    _ => value // No conversion for other types
                };
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(ex.Message + "; Value casted: " + value, ex);
            }
        }

        //private static void BufferQueryLog(string query, Dictionary<string, object> parameters, long executionTimeMs, byte retryCount, bool success)
        //{
        //    // Normalize the query string by replacing multiple whitespace/newlines with a single space
        //    string normalizedQuery = Regex.Replace(query, @"\s+", " ").Trim();
        //    queryLogBuffer.Add((normalizedQuery, parameters, executionTimeMs, retryCount, success, new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds()));

        //    if (queryLogBuffer.Count >= 60)
        //    {
        //        FlushQueryLogBuffer().Wait();
        //    }
        //}

        //private static async Task FlushQueryLogBuffer()
        //{
        //    using SqlConnection connection = new(connectionString);
        //    connection.Open();

        //    StringBuilder logInsert = new StringBuilder("INSERT INTO lu_query_log (query_text, parameters_text, execution_time, retry_count, success, timestamp_utc) VALUES ");
        //    List<SqlParameter> parameters = new List<SqlParameter>();

        //    for (int i = 0; i < queryLogBuffer.Count; i++)
        //    {
        //        (string query, Dictionary<string, object?> parameters, long executionTimeMs, byte retryCount, bool success, long timestamp) log = queryLogBuffer[i];

        //        // Append SQL for this entry with unique parameter placeholders
        //        logInsert.Append($"(@query_text_{i}, @parameters_text_{i}, @execution_time_{i}, @retry_count_{i}, @success_{i}, @timestamp_utc_{i})");

        //        if (i < queryLogBuffer.Count - 1)
        //            logInsert.Append(", ");  // Add a comma between entries

        //        // Add parameters for this log entry
        //        parameters.Add(new SqlParameter($"@query_text_{i}", log.query));
        //        parameters.Add(new SqlParameter($"@parameters_text_{i}", string.Join(", ", log.parameters.Select(kvp => $"{kvp.Key}={kvp.Value ?? "NULL"}"))));
        //        parameters.Add(new SqlParameter($"@execution_time_{i}", log.executionTimeMs));
        //        parameters.Add(new SqlParameter($"@retry_count_{i}", log.retryCount));
        //        parameters.Add(new SqlParameter($"@success_{i}", log.success));
        //        parameters.Add(new SqlParameter($"@timestamp_utc_{i}", log.timestamp));
        //    }

        //    using SqlCommand command = new(logInsert.ToString(), connection);
        //    command.Parameters.AddRange(parameters.ToArray());

        //    // This insert shouldn't be logged into querylog
        //    const int maxRetries = 3;
        //    int retryCount = 0;
        //    bool success = false;

        //    while (!success && retryCount < maxRetries)
        //    {
        //        try
        //        {
        //            command.ExecuteNonQuery();

        //            success = true;
        //        }
        //        catch (SqlException ex)
        //        {
        //            if (ex.ErrorCode == 5 || ex.ErrorCode == 6)
        //            {
        //                retryCount++;
        //                await Task.Delay(200); // wait before retrying
        //            }
        //            else
        //            {
        //                throw new InvalidOperationException(ex.Message + "; " + command.CommandText, ex);
        //            }
        //        }
        //    }

        //    queryLogBuffer.Clear();
        //    return;
        //}

        public static int ExecuteNonQuery(this SqlCommand command)
        {
            return ExecuteNonQueryAsync(command).Result;
        }

        private static async Task<int> ExecuteNonQueryAsync(this SqlCommand command)
        {
            const int maxRetries = 1;
            int retryCount = 0;
            bool success = false;
            int response = 0;
            long executionTimeMs = 0;

            // Prepare the parameters dictionary for logging
            Dictionary<string, object> parameters = command.Parameters
                .Cast<SqlParameter>()
                .ToDictionary(p => p.ParameterName, p => p.Value);

            while (!success && retryCount < maxRetries)
            {
                try
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    response = command.ExecuteNonQuery();
                    stopwatch.Stop();

                    executionTimeMs = stopwatch.ElapsedMilliseconds;
                    success = true;
                }
                catch (SqlException ex)
                {
                    if (ex.ErrorCode == 5 || ex.ErrorCode == 6)
                    {
                        retryCount++;
                        await Task.Delay(200); // wait before retrying
                    }
                    else
                    {
                        throw new InvalidOperationException(ex.Message + "; " + command.CommandText, ex);
                    }
                }
            }

            // Log the query attempt
            //BufferQueryLog(command.CommandText, parameters, executionTimeMs, (byte)retryCount, success);

            if (!success)
            {
                throw new Exception("Failed to execute query due to database lock.");
            }

            return response;
        }

        public static object? ExecuteScalar(this SqlCommand command)
        {
            return ExecuteScalarAsync(command).Result;
        }

        private static async Task<object?> ExecuteScalarAsync(this SqlCommand command)
        {
            const int maxRetries = 1;
            int retryCount = 0;
            bool success = false;
            object? response = null;
            long executionTimeMs = 0;

            // Prepare the parameters dictionary for logging
            Dictionary<string, object> parameters = command.Parameters
                .Cast<SqlParameter>()
                .ToDictionary(p => p.ParameterName, p => p.Value);

            while (!success && retryCount < maxRetries)
            {
                try
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    response = await command.ExecuteScalarAsync();
                    stopwatch.Stop();

                    executionTimeMs = stopwatch.ElapsedMilliseconds;
                    success = true;
                }
                catch (SqlException ex)
                {
                    if (ex.ErrorCode == 5 || ex.ErrorCode == 6)
                    {
                        retryCount++;
                        await Task.Delay(200); // wait before retrying
                    }
                    else
                    {
                        throw new InvalidOperationException(ex.Message + "; " + command.CommandText, ex);
                    }
                }
            }

            // Log the query attempt
            //BufferQueryLog(command.CommandText, parameters, executionTimeMs, (byte)retryCount, success);

            if (!success)
            {
                throw new Exception("Failed to execute scalar due to database lock.");
            }

            return response;
        }

        public static SqlDataReader ExecuteReader(this SqlCommand command)
        {
            return ExecuteReaderAsync(command).Result;
        }

        private static async Task<SqlDataReader> ExecuteReaderAsync(this SqlCommand command)
        {
            const int maxRetries = 1;
            int retryCount = 0;
            bool success = false;
            SqlDataReader? response = null;
            long executionTimeMs = 0;

            // Prepare the parameters dictionary for logging
            Dictionary<string, object> parameters = command.Parameters
                .Cast<SqlParameter>()
                .ToDictionary(p => p.ParameterName, p => p.Value);

            while (!success && retryCount < maxRetries)
            {
                try
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    response = command.ExecuteReader();
                    stopwatch.Stop();

                    executionTimeMs = stopwatch.ElapsedMilliseconds;
                    success = true;
                }
                catch (SqlException ex)
                {
                    if (ex.ErrorCode == 5 || ex.ErrorCode == 6)
                    {
                        retryCount++;
                        await Task.Delay(200); // wait before retrying
                    }
                    else
                    {
                        throw new InvalidOperationException(ex.Message + "; " + command.CommandText, ex);
                    }
                }
            }

            // Log the query attempt
            //BufferQueryLog(command.CommandText, parameters, executionTimeMs, (byte)retryCount, success);

            if (!success)
            {
                throw new Exception("Failed to execute reader due to database lock.");
            }

            return response;
        }
    }
}
