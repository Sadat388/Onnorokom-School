using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;
using Xunit;

// Shared fixture: one real PostgreSQL container and a controllable clock.
public class ApiFixture : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-11-01T04:00:00Z"));

    public ApiFixture() => _db.StartAsync().GetAwaiter().GetResult();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("HOLD_DURATION_SECONDS", "600");
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _db.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}

public class AdmissionTests(ApiFixture factory) : IClassFixture<ApiFixture>
{
    private const string StudentA = "20152691234567890";
    private const string StudentB = "20152691234567891";
    private readonly HttpClient _client = factory.CreateClient();

    private static HttpRequestMessage Req(HttpMethod method, string url, string studentId)
    {
        var r = new HttpRequestMessage(method, url);
        r.Headers.Add("X-Student-Id", studentId);
        return r;
    }

    private async Task<string> CreateClassAsync(int seats)
    {
        var r = await _client.PostAsJsonAsync("/classes",
            new { name = $"Class {Guid.NewGuid():N}", totalSeats = seats });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Concurrent_holds_never_exceed_total_seats()
    {
        var classId = await CreateClassAsync(10);
        var students = Enumerable.Range(0, 50).Select(i => $"2015269123456{i:D4}").ToList();

        var responses = await Task.WhenAll(students.Select(s =>
            _client.SendAsync(Req(HttpMethod.Post, $"/classes/{classId}/holds", s))));

        Assert.Equal(10, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(40, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var cls = await _client.GetFromJsonAsync<JsonElement>($"/classes/{classId}");
        Assert.Equal(10, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Expired_hold_releases_seat_automatically()
    {
        var classId = await CreateClassAsync(1);

        var a = await _client.SendAsync(Req(HttpMethod.Post, $"/classes/{classId}/holds", StudentA));
        Assert.Equal(HttpStatusCode.Created, a.StatusCode);
        var holdA = (await a.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("holdId").GetString()!;

        factory.Clock.Advance(TimeSpan.FromSeconds(601));

        var b = await _client.SendAsync(Req(HttpMethod.Post, $"/classes/{classId}/holds", StudentB));
        Assert.Equal(HttpStatusCode.Created, b.StatusCode); // seat freed without any call

        var status = await _client.SendAsync(Req(HttpMethod.Get, $"/holds/{holdA}", StudentA));
        var body = await status.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EXPIRED", body.GetProperty("status").GetString());

        var confirm = await _client.SendAsync(Req(HttpMethod.Post, $"/holds/{holdA}/confirm", StudentA));
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
    }

    [Fact]
    public async Task One_active_application_per_student_and_idempotent_confirm()
    {
        var classId = await CreateClassAsync(5);

        var first = await _client.SendAsync(Req(HttpMethod.Post, $"/classes/{classId}/holds", StudentA));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var holdId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("holdId").GetString()!;

        var second = await _client.SendAsync(Req(HttpMethod.Post, $"/classes/{classId}/holds", StudentA));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var other = await _client.SendAsync(Req(HttpMethod.Post, $"/holds/{holdId}/confirm", StudentB));
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode); // not your hold

        var c1 = await _client.SendAsync(Req(HttpMethod.Post, $"/holds/{holdId}/confirm", StudentA));
        var c2 = await _client.SendAsync(Req(HttpMethod.Post, $"/holds/{holdId}/confirm", StudentA));
        Assert.Equal(HttpStatusCode.OK, c1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, c2.StatusCode);

        var cls = await _client.GetFromJsonAsync<JsonElement>($"/classes/{classId}");
        Assert.Equal(1, cls.GetProperty("confirmedSeats").GetInt32());
    }
}