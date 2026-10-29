using System.Diagnostics;
using System.Net.Http.Json;

string name = Environment.GetEnvironmentVariable("CLIENT_NAME") ?? "client";
string url = Environment.GetEnvironmentVariable("TIME_SERVER_URL")
    ?? "http://localhost:8080/api/time";

int offsetSeconds = int.TryParse(
    Environment.GetEnvironmentVariable("CLOCK_OFFSET_SECONDS"), out int offset)
    ? offset : 0;

int samples = int.TryParse(
    Environment.GetEnvironmentVariable("SAMPLES"), out int count)
    ? Math.Max(1, count) : 5;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

TimeSpan simulatedOffset = TimeSpan.FromSeconds(offsetSeconds);

Console.WriteLine($"[{name}] Servidor: {url}");
Console.WriteLine($"[{name}] Desfase inicial simulado: {simulatedOffset.TotalSeconds:F3} s");

for (int i = 1; i <= samples; i++)
{
    try
    {
        DateTimeOffset before = DateTimeOffset.UtcNow + simulatedOffset;
        var stopwatch = Stopwatch.StartNew();
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        TimeResponse? data = await response.Content.ReadFromJsonAsync<TimeResponse>();
        stopwatch.Stop();

        if (data is null)
            throw new InvalidOperationException("Respuesta sin hora válida.");

        TimeSpan rtt = stopwatch.Elapsed;
        DateTimeOffset estimatedServerNow = data.UtcTime + TimeSpan.FromTicks(rtt.Ticks / 2);

        DateTimeOffset localNow = DateTimeOffset.UtcNow + simulatedOffset;
        TimeSpan correction = estimatedServerNow - localNow;

        simulatedOffset += correction;
        DateTimeOffset corrected = DateTimeOffset.UtcNow + simulatedOffset;

        Console.WriteLine($"\n[{name}] Muestra {i}/{samples}");
        Console.WriteLine($"  Hora local antes: {before:O}");
        Console.WriteLine($"  Hora del servidor: {data.UtcTime:O}");
        Console.WriteLine($"  RTT: {rtt.TotalMilliseconds:F2} ms");
        Console.WriteLine($"  Hora estimada del servidor: {estimatedServerNow:O}");
        Console.WriteLine($"  Corrección aplicada: {correction.TotalMilliseconds:F2} ms");
        Console.WriteLine($"  Hora local corregida: {corrected:O}");

        if (i < samples)
            await Task.Delay(TimeSpan.FromSeconds(3));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{name}] Error en muestra {i}: {ex.Message}");
    }
}

public record TimeResponse(DateTimeOffset UtcTime);