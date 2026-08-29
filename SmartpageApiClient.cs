using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmartpageTimetableDuplicateV1
{
    // Vékony, UI-mentes wrapper egy Smartpage szerver-kapcsolat köré (base URL + hitelesített
    // HttpClient): csak azt tudja, hogyan kell egy endpointot lekérdezni és a JSON választ
    // értelmezni. A hibák állapotüzenetté alakítását (SetStatus) a MainForm végzi.
    public class SmartpageApiClient
    {
        public HttpClient Http { get; }
        public string BaseUrl { get; }

        public SmartpageApiClient(HttpClient http, string baseUrl)
        {
            Http = http;
            BaseUrl = baseUrl;
        }

        public async Task<ApiResult<List<T>>> LoadListAsync<T>(string endpoint, Func<string, List<T>?> deserializer)
        {
            string fullEndpoint = $"{BaseUrl}/{endpoint}";
            try
            {
                HttpResponseMessage resp = await Http.GetAsync(fullEndpoint);
                if (!resp.IsSuccessStatusCode)
                {
                    string err = await resp.Content.ReadAsStringAsync();
                    return ApiResult<List<T>>.Fail($"{resp.StatusCode} - {err}");
                }

                string body = await resp.Content.ReadAsStringAsync();
                var list = deserializer(body);
                return list == null
                    ? ApiResult<List<T>>.Fail("a válasz nem értelmezhető")
                    : ApiResult<List<T>>.Ok(list);
            }
            catch (Exception ex)
            {
                return ApiResult<List<T>>.Fail(ex.Message);
            }
        }
    }

    public class ApiResult<T>
    {
        public bool Success { get; private init; }
        public T? Value { get; private init; }
        public string? Error { get; private init; }

        public static ApiResult<T> Ok(T value) => new() { Success = true, Value = value };
        public static ApiResult<T> Fail(string error) => new() { Success = false, Error = error };
    }
}
