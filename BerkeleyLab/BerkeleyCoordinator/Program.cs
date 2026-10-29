using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

var nodes = (Environment.GetEnvironmentVariable("NODES") ??
             "http://node1:8080,http://node2:8080,http://node3:8080")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

int intervalSeconds = int.TryParse(Environment.GetEnvironmentVariable("SYNC_INTERVAL_SECONDS"), out var i)
    ? Math.Max(1, i) : 15;

double ownOffsetMs = double.TryParse(
    Environment.GetEnvironmentVariable("INITIAL_OFFSET_MS"),
    NumberStyles.Float, CultureInfo.InvariantCulture, out var initial)
    ? initial : 0;

double outlierThresholdMs = double.TryParse(
    Environment.GetEnvironmentVariable("OUTLIER_THRESHOLD_MS"),
    NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
    ? Math.Max(0, threshold) : 60_000;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };

Console.WriteLine("Coordinador Berkeley iniciado.");

while (!stopping.IsCancellationRequested)
{
    try
    {
        var referenceUtc = DateTimeOffset.UtcNow;
        var coordinatorAtReference = referenceUtc.AddMilliseconds(ownOffsetMs);
        var measurements = new List<Measurement>();

        foreach (var node in nodes)
        {
            try
            {
                var before = DateTimeOffset.UtcNow;
                var sw = Stopwatch.StartNew();
                var sample = await http.GetFromJsonAsync<ClockSample>(
                    $"{node}/api/clock/delayed", stopping.Token);
                sw.Stop();
                var after = DateTimeOffset.UtcNow;

                if (sample is null) throw new Exception("Respuesta vacía");

                var estimatedRemoteAtReceive = sample.UtcTime.AddMilliseconds(
                    sw.Elapsed.TotalMilliseconds / 2.0);
                var estimatedRemoteAtReference = estimatedRemoteAtReceive - (after - referenceUtc);
                var differenceMs = (estimatedRemoteAtReference - coordinatorAtReference).TotalMilliseconds;

                measurements.Add(new Measurement(node, differenceMs, sw.Elapsed.TotalMilliseconds));
                Console.WriteLine($"{node}: diferencia={differenceMs:F1} ms, RTT={sw.Elapsed.TotalMilliseconds:F1} ms");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"{node}: no disponible ({ex.Message})");
            }
        }

        var accepted = measurements
            .Where(m => Math.Abs(m.DifferenceMs) <= outlierThresholdMs)
            .ToList();
        var rejected = measurements.Except(accepted).ToList();
        foreach (var m in rejected)
            Console.WriteLine($"Descartado por desfase excesivo: {m.Node}");

        if (accepted.Count == 0)
        {
            Console.WriteLine("Sin participantes válidos; se omite la ronda.");
        }
        else
        {
            double meanMs = accepted.Sum(m => m.DifferenceMs) / (accepted.Count + 1);
            Console.WriteLine($"Promedio de diferencias: {meanMs:F1} ms");

            foreach (var m in accepted)
            {
                double correctionMs = meanMs - m.DifferenceMs;
                try
                {
                    using var response = await http.PostAsJsonAsync(
                        $"{m.Node}/api/clock/adjust",
                        new { DeltaMs = correctionMs }, stopping.Token);
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine($"Corrección enviada a {m.Node}: {correctionMs:F1} ms");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"Error al ajustar {m.Node}: {ex.Message}");
                }
            }

            ownOffsetMs += meanMs;
            Console.WriteLine($"Nuevo offset coordinador: {ownOffsetMs:F1} ms");
        }
    }
    catch (OperationCanceledException) when (stopping.IsCancellationRequested)
    {
        break;
    }

    try { await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stopping.Token); }
    catch (OperationCanceledException) { break; }
}

public sealed record ClockSample(
    [property: JsonPropertyName("node")] string Node,
    [property: JsonPropertyName("utcTime")] DateTimeOffset UtcTime,
    [property: JsonPropertyName("offsetMs")] double OffsetMs);

public sealed record Measurement(string Node, double DifferenceMs, double RttMs);