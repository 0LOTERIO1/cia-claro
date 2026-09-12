using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Repositories;
using Cia.Api.Services;

namespace Cia.Api.Tests;

public class ServiceRatingTests
{
    [Fact]
    public async Task Customer_RatesOwnResolvedSession()
    {
        using var db = TestComposition.CreateDb();
        var session = await CreateResolvedSessionAsync(db);
        var ratings = TestComposition.CreateRatings(db);

        var result = await ratings.SubmitAsync(DbSeeder.DemoCustomerId, session.Id, new SubmitServiceRatingRequest
        {
            Score = 4,
            Comment = "  Atendimento muito bom.  "
        });

        Assert.True(result.Success);
        Assert.Equal("Obrigado pela sua avaliação.", result.Message);
        Assert.Equal(4, result.Rating?.Score);
        Assert.Equal("Atendimento muito bom.", result.Rating?.Comment);
        Assert.Single(db.ServiceRatings);
        Assert.Equal(session.Id, db.ServiceRatings.Single().SessionId);
        Assert.Equal(DbSeeder.DemoCustomerId, db.ServiceRatings.Single().CustomerId);
    }

    [Fact]
    public async Task Score_One_IsAccepted()
    {
        using var db = TestComposition.CreateDb();
        var session = await CreateResolvedSessionAsync(db);
        var ratings = TestComposition.CreateRatings(db);

        var result = await ratings.SubmitAsync(DbSeeder.DemoCustomerId, session.Id, new SubmitServiceRatingRequest
        {
            Score = 1
        });

        Assert.True(result.Success);
        Assert.Equal(1, db.ServiceRatings.Single().Score);
        Assert.Null(db.ServiceRatings.Single().Comment);
    }

    [Fact]
    public async Task Score_Five_IsAccepted()
    {
        using var db = TestComposition.CreateDb();
        var session = await CreateResolvedSessionAsync(db);
        var ratings = TestComposition.CreateRatings(db);

        var result = await ratings.SubmitAsync(DbSeeder.DemoCustomerId, session.Id, new SubmitServiceRatingRequest
        {
            Score = 5
        });

        Assert.True(result.Success);
        Assert.Equal(5, db.ServiceRatings.Single().Score);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Score_OutOfRange_IsRejected(int score)
    {
        using var db = TestComposition.CreateDb();
        var session = await CreateResolvedSessionAsync(db);
        var ratings = TestComposition.CreateRatings(db);

        var error = await Assert.ThrowsAsync<ValidationAppException>(() =>
            ratings.SubmitAsync(DbSeeder.DemoCustomerId, session.Id, new SubmitServiceRatingRequest { Score = score }));

        Assert.Contains("1 e 5", error.Message);
        Assert.Empty(db.ServiceRatings);
    }

    [Fact]
    public async Task Customer_CannotRateActiveSession()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, _, _, _) = TestComposition.CreateServices(db);
        var created = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Minha internet não está funcionando."
        });
        var ratings = TestComposition.CreateRatings(db);

        var error = await Assert.ThrowsAsync<ValidationAppException>(() =>
            ratings.SubmitAsync(DbSeeder.DemoCustomerId, created.SessionId, new SubmitServiceRatingRequest { Score = 5 }));

        Assert.Contains("finalizado", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(db.ServiceRatings);
        Assert.False((await TestComposition.CreateIdentities(db).GetActiveSessionAsync(DbSeeder.DemoCustomerId)).CanRate);
    }

    [Fact]
    public async Task Customer_CannotRateAnotherCustomersSession()
    {
        using var db = TestComposition.CreateDb();
        db.Customers.Add(new Customer
        {
            Id = DbSeeder.PedroCustomerId,
            Name = "Pedro",
            Phone = "11988887777",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var session = await CreateResolvedSessionAsync(db);
        var ratings = TestComposition.CreateRatings(db);

        var error = await Assert.ThrowsAsync<ValidationAppException>(() =>
            ratings.SubmitAsync(DbSeeder.PedroCustomerId, session.Id, new SubmitServiceRatingRequest { Score = 5 }));

        Assert.Contains("não pode avaliar", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(db.ServiceRatings);
    }

    [Fact]
    public async Task Session_CannotBeRatedTwice()
    {
        using var db = TestComposition.CreateDb();
        var session = await CreateResolvedSessionAsync(db);
        var ratings = TestComposition.CreateRatings(db);
        await ratings.SubmitAsync(DbSeeder.DemoCustomerId, session.Id, new SubmitServiceRatingRequest { Score = 5 });

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            ratings.SubmitAsync(DbSeeder.DemoCustomerId, session.Id, new SubmitServiceRatingRequest { Score = 4 }));

        Assert.Contains("já foi avaliado", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(db.ServiceRatings);
        Assert.Equal(5, db.ServiceRatings.Single().Score);
    }

    [Fact]
    public async Task OptionalComment_CanBeOmitted()
    {
        using var db = TestComposition.CreateDb();
        var session = await CreateResolvedSessionAsync(db);
        var ratings = TestComposition.CreateRatings(db);

        var result = await ratings.SubmitAsync(DbSeeder.DemoCustomerId, session.Id, new SubmitServiceRatingRequest
        {
            Score = 3,
            Comment = "   "
        });

        Assert.True(result.Success);
        Assert.Null(result.Rating?.Comment);
        Assert.Null(db.ServiceRatings.Single().Comment);
    }

    [Fact]
    public async Task Comment_OverLimit_IsRejected()
    {
        using var db = TestComposition.CreateDb();
        var session = await CreateResolvedSessionAsync(db);
        var ratings = TestComposition.CreateRatings(db);

        var error = await Assert.ThrowsAsync<ValidationAppException>(() =>
            ratings.SubmitAsync(DbSeeder.DemoCustomerId, session.Id, new SubmitServiceRatingRequest
            {
                Score = 5,
                Comment = new string('a', ServiceRatingService.MaxCommentLength + 1)
            }));

        Assert.Contains("1000", error.Message);
        Assert.Empty(db.ServiceRatings);
    }

    [Fact]
    public async Task Dashboard_CalculatesAverageCountAndDistribution()
    {
        using var db = TestComposition.CreateDb();
        await SeedRatedSessionAsync(db, "CIA-R1", 5);
        await SeedRatedSessionAsync(db, "CIA-R2", 5);
        await SeedRatedSessionAsync(db, "CIA-R3", 4);
        await CreateResolvedSessionAsync(db, "CIA-R4");

        var dashboard = await TestComposition.CreateDashboard(db).GetDashboardAsync();

        Assert.Equal(4.7m, dashboard.AverageScore);
        Assert.Equal(3, dashboard.RatedSessions);
        Assert.Equal(75m, dashboard.RatingRate);
        Assert.Equal(0, dashboard.ScoreDistribution.Single(d => d.Score == 1).Count);
        Assert.Equal(0, dashboard.ScoreDistribution.Single(d => d.Score == 2).Count);
        Assert.Equal(0, dashboard.ScoreDistribution.Single(d => d.Score == 3).Count);
        Assert.Equal(1, dashboard.ScoreDistribution.Single(d => d.Score == 4).Count);
        Assert.Equal(2, dashboard.ScoreDistribution.Single(d => d.Score == 5).Count);
    }

    [Fact]
    public async Task Dashboard_WithoutRatings_ReturnsEmptyMetrics()
    {
        using var db = TestComposition.CreateDb();
        var dashboard = await TestComposition.CreateDashboard(db).GetDashboardAsync();

        Assert.Null(dashboard.AverageScore);
        Assert.Equal(0, dashboard.RatedSessions);
        Assert.Equal(0m, dashboard.RatingRate);
        Assert.Equal(5, dashboard.ScoreDistribution.Count);
        Assert.All(dashboard.ScoreDistribution, item => Assert.Equal(0, item.Count));
    }

    [Fact]
    public async Task Admin_CanViewSessionRating()
    {
        using var db = TestComposition.CreateDb();
        var session = await CreateResolvedSessionAsync(db);
        await TestComposition.CreateRatings(db).SubmitAsync(
            DbSeeder.DemoCustomerId,
            session.Id,
            new SubmitServiceRatingRequest { Score = 5, Comment = "Atendimento rápido e claro." });

        var detail = await TestComposition.CreateDashboard(db).GetSessionDetailAsync(session.Id);

        Assert.False(detail.Session.CanRate);
        Assert.Equal(5, detail.Rating?.Score);
        Assert.Equal("Atendimento rápido e claro.", detail.Rating?.Comment);
        Assert.Equal(5, detail.Session.Rating?.Score);
        Assert.Contains(await TestComposition.CreateDashboard(db).GetSessionsAsync(), item => item.Rating?.Score == 5);
    }

    [Fact]
    public async Task Agent_CannotCreateRating_AndFinishDoesNotRate()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, _, humanAgent, _) = TestComposition.CreateServices(db);
        var agent = TestComposition.SeedAgent(db);

        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Minha internet não está funcionando."
        });
        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Quero falar com um atendente"
        });

        var queue = await humanAgent.GetQueueAsync();
        await humanAgent.AssumeAsync(queue[0].RequestId, agent.Id);
        var finished = await humanAgent.FinishAsync(queue[0].RequestId, agent.Id);

        Assert.Equal(SessionStatus.Resolved, finished.Session.Status);
        Assert.True(finished.Session.CanRate);
        Assert.Null(finished.Session.Rating);
        Assert.Empty(db.ServiceRatings);

        var error = await Assert.ThrowsAsync<ValidationAppException>(() =>
            TestComposition.CreateRatings(db).SubmitAsync(agent.Id.ToString(), finished.Session.Id, new SubmitServiceRatingRequest
            {
                Score = 5
            }));

        Assert.Contains("não pode avaliar", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(db.ServiceRatings);
    }

    [Fact]
    public async Task TelegramSessionLinkedToPortal_CanBeRated()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);

        await inbound.HandleUpdateAsync(CreateTelegramUpdate("Minha internet nao esta funcionando"));
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateTelegramUpdate($"/link {generated.Code}"));

        var session = db.ConversationSessions.Single();
        Assert.Equal(DbSeeder.DemoCustomerId, session.CustomerId);
        Assert.Equal(ChannelType.Telegram, session.InitialChannel);

        session.Status = SessionStatus.Resolved;
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var snapshot = await identities.GetActiveSessionAsync(DbSeeder.DemoCustomerId);
        Assert.Equal(session.Id, snapshot.Session?.Id);
        Assert.True(snapshot.CanRate);
        Assert.Null(snapshot.Rating);

        var result = await TestComposition.CreateRatings(db).SubmitAsync(
            DbSeeder.DemoCustomerId,
            session.Id,
            new SubmitServiceRatingRequest { Score = 5, Comment = "Continuei no Portal e avaliei." });

        Assert.True(result.Success);
        Assert.Equal(ChannelType.Telegram, db.ServiceRatings.Single().ChannelAtCompletion);

        var after = await identities.GetActiveSessionAsync(DbSeeder.DemoCustomerId);
        Assert.False(after.CanRate);
        Assert.Equal(5, after.Rating?.Score);
    }

    private static TelegramUpdateDto CreateTelegramUpdate(string text)
    {
        return new TelegramUpdateDto
        {
            UpdateId = 1,
            Message = new TelegramMessageDto
            {
                MessageId = 10,
                Text = text,
                From = new TelegramUserDto
                {
                    Id = 99887766,
                    FirstName = "Lucas",
                    Username = "lucas_claro"
                },
                Chat = new TelegramChatDto
                {
                    Id = 99887766,
                    Type = "private"
                }
            }
        };
    }

    private static async Task<ConversationSession> CreateResolvedSessionAsync(AppDbContext db, string? protocol = null)
    {
        var (conversation, _, _, _) = TestComposition.CreateServices(db);
        var created = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Minha internet não está funcionando."
        });

        var session = await db.ConversationSessions.FindAsync(created.SessionId)
            ?? throw new InvalidOperationException("Sessão não criada.");
        if (protocol is not null)
        {
            session.Protocol = protocol;
        }

        session.Status = SessionStatus.Resolved;
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return session;
    }

    private static async Task SeedRatedSessionAsync(AppDbContext db, string protocol, int score)
    {
        var session = await CreateResolvedSessionAsync(db, protocol);
        await TestComposition.CreateRatings(db).SubmitAsync(
            DbSeeder.DemoCustomerId,
            session.Id,
            new SubmitServiceRatingRequest { Score = score });
    }
}
