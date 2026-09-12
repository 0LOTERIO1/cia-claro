using System.Security.Cryptography;
using System.Text;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class ChannelIdentityService : IChannelIdentityService
{
    public const int LinkCodeMinutes = 10;

    private readonly ICustomerRepository _customers;
    private readonly ISessionRepository _sessions;
    private readonly IMessageRepository _messages;
    private readonly IChannelIdentityRepository _identities;
    private readonly IChannelLinkCodeRepository _codes;
    private readonly ILogger<ChannelIdentityService> _logger;

    public ChannelIdentityService(
        ICustomerRepository customers,
        ISessionRepository sessions,
        IMessageRepository messages,
        IChannelIdentityRepository identities,
        IChannelLinkCodeRepository codes,
        ILogger<ChannelIdentityService> logger)
    {
        _customers = customers;
        _sessions = sessions;
        _messages = messages;
        _identities = identities;
        _codes = codes;
        _logger = logger;
    }

    public async Task<Customer> GetOrCreateTelegramCustomerAsync(
        long telegramUserId,
        long telegramChatId,
        string? firstName,
        CancellationToken cancellationToken = default)
    {
        var externalUserId = telegramUserId.ToString();
        var identity = await _identities.GetByChannelUserAsync(ChannelType.Telegram, externalUserId, cancellationToken);
        if (identity is not null)
        {
            var linked = identity.Customer;
            var changed = false;
            if (identity.ExternalChatId != telegramChatId.ToString())
            {
                identity.ExternalChatId = telegramChatId.ToString();
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(firstName) && identity.DisplayName != firstName.Trim())
            {
                identity.DisplayName = firstName.Trim();
                changed = true;
            }

            if (linked.TelegramUserId != telegramUserId || linked.TelegramChatId != telegramChatId)
            {
                linked.TelegramUserId = telegramUserId;
                linked.TelegramChatId = telegramChatId;
                changed = true;
            }

            if (changed)
            {
                await _sessions.SaveChangesAsync(cancellationToken);
            }

            return linked;
        }

        var legacy = await _customers.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (legacy is not null)
        {
            await EnsureIdentityAsync(legacy, telegramUserId, telegramChatId, firstName, cancellationToken);
            if (legacy.TelegramChatId != telegramChatId)
            {
                legacy.TelegramChatId = telegramChatId;
                await _customers.SaveChangesAsync(cancellationToken);
            }

            return legacy;
        }

        var temporaryCustomerId = $"TG-{telegramUserId}";
        var existingTemporary = await _customers.GetByIdAsync(temporaryCustomerId, cancellationToken);
        if (existingTemporary is not null)
        {
            RestoreTemporaryTelegramCustomer(existingTemporary, telegramUserId, telegramChatId, firstName);
            await EnsureIdentityAsync(existingTemporary, telegramUserId, telegramChatId, firstName, cancellationToken);
            await _customers.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Reused orphan Telegram customer. CustomerId={CustomerId} TelegramUserId={TelegramUserId}",
                existingTemporary.Id, telegramUserId);
            return existingTemporary;
        }

        var name = string.IsNullOrWhiteSpace(firstName) ? $"Cliente {telegramUserId}" : firstName.Trim();
        var customer = new Customer
        {
            Id = temporaryCustomerId,
            Name = name,
            Phone = TruncatePhone(telegramUserId.ToString()),
            TelegramUserId = telegramUserId,
            TelegramChatId = telegramChatId,
            CreatedAt = DateTime.UtcNow
        };

        await _customers.AddAsync(customer, cancellationToken);
        await _identities.AddAsync(new CustomerChannelIdentity
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Channel = ChannelType.Telegram,
            ExternalUserId = externalUserId,
            ExternalChatId = telegramChatId.ToString(),
            DisplayName = name,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _customers.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task<long?> GetTelegramChatIdAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var identities = await _identities.GetByCustomerIdAsync(customerId, cancellationToken);
        var telegram = identities.FirstOrDefault(x => x.Channel == ChannelType.Telegram);
        if (telegram?.ExternalChatId is not null && long.TryParse(telegram.ExternalChatId, out var chatId))
        {
            return chatId;
        }

        var customer = await _customers.GetByIdAsync(customerId, cancellationToken);
        return customer?.TelegramChatId;
    }

    public async Task<IReadOnlyList<CustomerChannelDto>> ListChannelsAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var identities = await _identities.GetByCustomerIdAsync(customerId, cancellationToken);
        var telegram = identities.FirstOrDefault(x => x.Channel == ChannelType.Telegram);
        return
        [
            new CustomerChannelDto
            {
                Channel = ChannelType.Telegram,
                Connected = telegram is not null,
                DisplayName = telegram?.DisplayName,
                VerifiedAt = telegram?.VerifiedAt ?? telegram?.CreatedAt
            }
        ];
    }

    public async Task<TelegramLinkCodeDto> GenerateTelegramLinkCodeAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        _ = await _customers.GetByIdAsync(customerId, cancellationToken)
            ?? throw new NotFoundException("Cliente não encontrado.");

        var pending = await _codes.GetPendingAsync(customerId, ChannelType.Telegram, cancellationToken);
        foreach (var previous in pending)
        {
            previous.ExpiresAt = DateTime.UtcNow;
        }

        var code = CreateCode();
        var expiresAt = DateTime.UtcNow.AddMinutes(LinkCodeMinutes);
        await _codes.AddAsync(new ChannelLinkCode
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Channel = ChannelType.Telegram,
            CodeHash = HashCode(code),
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _sessions.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Telegram link code generated. CustomerId={CustomerId} ExpiresAt={ExpiresAt}",
            customerId, expiresAt);

        return new TelegramLinkCodeDto
        {
            Code = code,
            Command = $"/link {code}",
            ExpiresAt = expiresAt,
            ExpiresInMinutes = LinkCodeMinutes
        };
    }

    public async Task<string> RedeemTelegramLinkAsync(
        string code,
        long telegramUserId,
        long telegramChatId,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.StartsWith("CIA-", StringComparison.Ordinal))
        {
            throw new ValidationAppException("Envie o comando no formato /link CIA-000000.");
        }

        var record = await _codes.GetLatestByHashAsync(HashCode(normalized), cancellationToken);
        if (record is null)
        {
            throw new ValidationAppException("Código inválido ou expirado.");
        }

        if (record.UsedAt is not null)
        {
            throw new ValidationAppException("Este código já foi utilizado.");
        }

        if (record.ExpiresAt <= DateTime.UtcNow)
        {
            throw new ValidationAppException("Este código expirou. Gere um novo no Portal CIA.");
        }

        var portalCustomer = await _customers.GetByIdAsync(record.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Cliente do código não encontrado.");

        var externalUserId = telegramUserId.ToString();
        var portalIdentities = await _identities.GetByCustomerIdAsync(portalCustomer.Id, cancellationToken);
        var portalTelegram = portalIdentities.FirstOrDefault(x => x.Channel == ChannelType.Telegram);
        if (portalTelegram is not null && portalTelegram.ExternalUserId != externalUserId)
        {
            throw new ConflictException("Esta conta já possui outro Telegram vinculado.");
        }

        var identity = await _identities.GetByChannelUserAsync(ChannelType.Telegram, externalUserId, cancellationToken);
        if (identity is not null && identity.CustomerId != portalCustomer.Id)
        {
            if (!IsTemporaryCustomer(identity.CustomerId))
            {
                throw new ConflictException("Este Telegram já está vinculado a outra conta.");
            }

            await AdoptTemporaryCustomerAsync(identity.CustomerId, portalCustomer, cancellationToken);
            identity.CustomerId = portalCustomer.Id;
            identity.Customer = portalCustomer;
            identity.ExternalChatId = telegramChatId.ToString();
            identity.DisplayName = string.IsNullOrWhiteSpace(displayName) ? identity.DisplayName : displayName.Trim();
            identity.VerifiedAt = DateTime.UtcNow;
        }
        else if (identity is null)
        {
            var tempCustomer = await _customers.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
            if (tempCustomer is not null && tempCustomer.Id != portalCustomer.Id)
            {
                if (!IsTemporaryCustomer(tempCustomer.Id))
                {
                    throw new ConflictException("Este Telegram já está vinculado a outra conta.");
                }

                await AdoptTemporaryCustomerAsync(tempCustomer.Id, portalCustomer, cancellationToken);
            }

            await _identities.AddAsync(new CustomerChannelIdentity
            {
                Id = Guid.NewGuid(),
                CustomerId = portalCustomer.Id,
                Channel = ChannelType.Telegram,
                ExternalUserId = externalUserId,
                ExternalChatId = telegramChatId.ToString(),
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
                VerifiedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        else
        {
            identity.ExternalChatId = telegramChatId.ToString();
            identity.VerifiedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                identity.DisplayName = displayName.Trim();
            }
        }

        portalCustomer.TelegramUserId = telegramUserId;
        portalCustomer.TelegramChatId = telegramChatId;
        record.UsedAt = DateTime.UtcNow;
        await _sessions.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Telegram account linked. PortalCustomerId={CustomerId} TelegramUserId={TelegramUserId}",
            portalCustomer.Id, telegramUserId);

        return "Conta vinculada com sucesso. Seu histórico agora pode ser continuado pelo Portal CIA.";
    }

    public async Task<ActiveSessionResponse> GetActiveSessionAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        _ = await _customers.GetByIdAsync(customerId, cancellationToken)
            ?? throw new NotFoundException("Cliente não encontrado.");

        var session = await _sessions.GetOpenByCustomerIdAsync(customerId, cancellationToken);
        IReadOnlyList<MessageDto> messages = session is null
            ? Array.Empty<MessageDto>()
            : (await _messages.GetBySessionIdAsync(session.Id, cancellationToken)).Select(m => m.ToDto()).ToList();

        return new ActiveSessionResponse
        {
            Session = session?.ToDto(),
            Messages = messages,
            Channels = await ListChannelsAsync(customerId, cancellationToken)
        };
    }

    public async Task<ActiveSessionResponse> ResumeActiveSessionAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetOpenByCustomerIdAsync(customerId, cancellationToken);
        if (session is not null && session.CurrentChannel != ChannelType.WebPortal)
        {
            session.CurrentChannel = ChannelType.WebPortal;
            session.UpdatedAt = DateTime.UtcNow;
            await _sessions.SaveChangesAsync(cancellationToken);
        }

        return await GetActiveSessionAsync(customerId, cancellationToken);
    }

    public async Task<ChannelUnlinkDto> UnlinkTelegramAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetByIdAsync(customerId, cancellationToken)
            ?? throw new NotFoundException("Cliente não encontrado.");

        var identities = await _identities.GetByCustomerIdAsync(customerId, cancellationToken);
        var telegrams = identities
            .Where(x => x.Channel == ChannelType.Telegram && x.CustomerId == customer.Id)
            .ToList();

        var hadLink = telegrams.Count > 0 || customer.TelegramUserId is not null || customer.TelegramChatId is not null;
        foreach (var identity in telegrams)
        {
            _identities.Remove(identity);
        }

        customer.TelegramUserId = null;
        customer.TelegramChatId = null;

        var session = await _sessions.GetOpenByCustomerIdAsync(customer.Id, cancellationToken);
        if (session is not null && session.CurrentChannel == ChannelType.Telegram)
        {
            session.CurrentChannel = ChannelType.WebPortal;
            session.UpdatedAt = DateTime.UtcNow;
        }

        await _sessions.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Telegram identity unlinked. CustomerId={CustomerId}", customer.Id);

        return new ChannelUnlinkDto
        {
            Success = true,
            Message = hadLink
                ? "Telegram desconectado com sucesso."
                : "Nenhum Telegram estava conectado."
        };
    }

    private async Task AdoptTemporaryCustomerAsync(
        string temporaryCustomerId,
        Customer portalCustomer,
        CancellationToken cancellationToken)
    {
        var sessions = await _sessions.GetByCustomerIdAsync(temporaryCustomerId, cancellationToken);
        foreach (var session in sessions)
        {
            session.CustomerId = portalCustomer.Id;
            session.Customer = portalCustomer;
            if (session.Status is SessionStatus.Active or SessionStatus.WaitingForAgent or SessionStatus.Transferred)
            {
                session.UpdatedAt = DateTime.UtcNow;
            }
        }

        var remainingIdentities = await _identities.GetByCustomerIdAsync(temporaryCustomerId, cancellationToken);
        foreach (var leftover in remainingIdentities.Where(x => x.Channel != ChannelType.Telegram))
        {
            leftover.CustomerId = portalCustomer.Id;
            leftover.Customer = portalCustomer;
        }

        var temporary = await _customers.GetByIdAsync(temporaryCustomerId, cancellationToken);
        if (temporary is not null)
        {
            temporary.TelegramUserId = null;
            temporary.TelegramChatId = null;
        }

        await _sessions.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Temporary Telegram customer adopted. From={From} To={To} Sessions={Count}",
            temporaryCustomerId, portalCustomer.Id, sessions.Count);
    }

    private async Task EnsureIdentityAsync(
        Customer customer,
        long telegramUserId,
        long telegramChatId,
        string? firstName,
        CancellationToken cancellationToken)
    {
        var existing = await _identities.GetByChannelUserAsync(ChannelType.Telegram, telegramUserId.ToString(), cancellationToken);
        if (existing is not null)
        {
            return;
        }

        await _identities.AddAsync(new CustomerChannelIdentity
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Channel = ChannelType.Telegram,
            ExternalUserId = telegramUserId.ToString(),
            ExternalChatId = telegramChatId.ToString(),
            DisplayName = string.IsNullOrWhiteSpace(firstName) ? customer.Name : firstName.Trim(),
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _customers.SaveChangesAsync(cancellationToken);
    }

    private static void RestoreTemporaryTelegramCustomer(
        Customer customer,
        long telegramUserId,
        long telegramChatId,
        string? firstName)
    {
        customer.TelegramUserId = telegramUserId;
        customer.TelegramChatId = telegramChatId;
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            customer.Name = firstName.Trim();
        }
    }

    public static bool IsTemporaryCustomer(string customerId) =>
        customerId.StartsWith("TG-", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeCode(string code)
    {
        var trimmed = (code ?? string.Empty).Trim().ToUpperInvariant();
        return trimmed.Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    public static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeCode(code)));
        return Convert.ToHexString(bytes);
    }

    private static string CreateCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return $"CIA-{value:D6}";
    }

    private static string TruncatePhone(string value) => value.Length <= 20 ? value : value[..20];
}
