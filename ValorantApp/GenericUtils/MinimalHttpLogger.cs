/*
 * MIT License
 * 
 * Copyright (c) 2022 John Korsnes
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Logging;
using Microsoft.Extensions.Logging;
using ValorantApp.GenericExtensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseMinimalHttpLogger(this IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, ReplaceLoggingHttpMessageHandlerBuilderFilter>());
            return services;
        }
    }

    internal class ReplaceLoggingHttpMessageHandlerBuilderFilter : IHttpMessageHandlerBuilderFilter
    {
        private readonly ILoggerFactory _loggerFactory;

        public ReplaceLoggingHttpMessageHandlerBuilderFilter(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
        {
            return builder =>
            {
                next(builder);

                var loggerName = !string.IsNullOrEmpty(builder.Name) ? builder.Name : "Default";
                ILogger<RequestEndOnlyLogger> innerLogger = _loggerFactory.CreateLogger<RequestEndOnlyLogger>();
                var toRemove = builder.AdditionalHandlers.Where(h => (h is LoggingHttpMessageHandler) || h is LoggingScopeHttpMessageHandler).Select(h => h).ToList();
                foreach (var delegatingHandler in toRemove)
                {
                    builder.AdditionalHandlers.Remove(delegatingHandler);
                }
                builder.AdditionalHandlers.Add(new RequestEndOnlyLogger(innerLogger));
            };
        }
    }

    internal class RequestEndOnlyLogger : DelegatingHandler
    {
        private readonly ILogger<RequestEndOnlyLogger> _logger;

        public RequestEndOnlyLogger(ILogger<RequestEndOnlyLogger> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            var requestUri = request.RequestUri?.ToString();
            var stopwatch = ValueStopwatch.StartNew();
            try
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger.ApiInformation($"{request.Method} {requestUri} - {(int)response.StatusCode} {response.StatusCode} in {stopwatch.GetElapsedTime().TotalMilliseconds}ms. ({response.GetResponseSize().Result.FormatSize()})");
                }
                else
                {
                    _logger.ApiWarning($"{request.Method} {requestUri} - {(int)response.StatusCode} {response.StatusCode} in {stopwatch.GetElapsedTime().TotalMilliseconds}ms. ({response.GetResponseSize().Result.FormatSize()})" +
                        $"\n{response}");
                }
                return response;
            }
            catch (Exception)
            {
                _logger.ApiError($"{request.Method} {requestUri} failed to respond in {stopwatch.GetElapsedTime().TotalMilliseconds}ms.");
                throw;
            }
        }

        internal struct ValueStopwatch
        {
            private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

            private long _startTimestamp;

            public bool IsActive => _startTimestamp != 0;

            private ValueStopwatch(long startTimestamp)
            {
                _startTimestamp = startTimestamp;
            }

            public static ValueStopwatch StartNew() => new ValueStopwatch(Stopwatch.GetTimestamp());

            public TimeSpan GetElapsedTime()
            {
                if (!IsActive)
                {
                    throw new InvalidOperationException("An uninitialized, or 'default', ValueStopwatch cannot be used to get elapsed time.");
                }

                long end = Stopwatch.GetTimestamp();
                long timestampDelta = end - _startTimestamp;
                long ticks = (long)(TimestampToTicks * timestampDelta);
                return new TimeSpan(ticks);
            }
        }
    }
}
