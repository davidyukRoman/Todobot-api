using System.Text;
using Application.Tasks.Commands;
using Application.Tasks.Queries;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TodoBot.Api;

public class BotHostedService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<BotHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public BotHostedService(
        ILogger<BotHostedService> logger, 
        IServiceScopeFactory scopeFactory, 
        IConfiguration configuration) 
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;

        // Дістаємо токен з конфігурації
        var botToken = _configuration["Telegram:BotToken"];
        
        if (string.IsNullOrEmpty(botToken))
        {
            throw new ArgumentNullException(nameof(botToken), "Telegram Bot Token is not configured!");
        }

        _botClient = new TelegramBotClient(botToken); 
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        _logger.LogInformation("Bot started receiving updates.");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is { } callbackQuery)
        {
            await HandleCallbackAsync(botClient, callbackQuery, cancellationToken);
            return;
        }
        
        if (update.Message is not { } message || message.Text is not { } messageText) return;

        var chatId = message.Chat.Id;
        var parts = messageText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts.Length > 0 ? parts[0].ToLower() : string.Empty;
        var payload = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        if (command == "/add" && !string.IsNullOrWhiteSpace(payload))
        {
            var responseText = await mediator.Send(new CreateTaskCommand(chatId, payload), cancellationToken);
            await botClient.SendMessage(chatId, responseText, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
        }
        else if (command == "/list")
        {
            await SendOrEditTaskListAsync(botClient, mediator, chatId, null, cancellationToken);
        }
        else if (command == "/start")
        {
            var webAppInfo = new WebAppInfo { Url = "https://todo-bot-ui.vercel.app/" }; 
            
            // Використовуємо InlineKeyboardMarkup замість ReplyKeyboardMarkup
            var markup = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithWebApp("📱 Відкрити To-Do List", webAppInfo)
            );

            await botClient.SendMessage(
                chatId: chatId, 
                text: "Привіт! Я твій To-Do бот.\nНатисни кнопку нижче, щоб відкрити зручний інтерфейс:", 
                replyMarkup: markup, 
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendMessage(chatId, "Невідома команда. Спробуй <b>/list</b> або <b>/add [текст]</b>.", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
        }
    }
    
    private async Task HandleCallbackAsync(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken ct)
    {
        if (callback.Data == null || callback.Message == null) return;
        
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var chatId = callback.Message.Chat.Id;
        var messageId = callback.Message.MessageId;
        
        if (callback.Data.StartsWith("t_") && Guid.TryParse(callback.Data.Substring(2), out var toggleId))
        {
            await mediator.Send(new ToggleTaskCommand(toggleId), ct);
        }
        else if (callback.Data.StartsWith("d_") && Guid.TryParse(callback.Data.Substring(2), out var deleteId))
        {
            await mediator.Send(new DeleteTaskCommand(deleteId), ct);
        }
        
        await botClient.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
        
        await SendOrEditTaskListAsync(botClient, mediator, chatId, messageId, ct);
    }
    
    private async Task SendOrEditTaskListAsync(ITelegramBotClient botClient, IMediator mediator, long chatId, int? messageId, CancellationToken ct)
    {
        var tasks = await mediator.Send(new GetTasksQuery(chatId), ct);

        if (!tasks.Any())
        {
            var emptyText = "📭 У вас немає завдань.";
            if (messageId.HasValue)
                await botClient.EditMessageText(chatId, messageId.Value, emptyText, parseMode: ParseMode.Html, cancellationToken: ct);
            else
                await botClient.SendMessage(chatId, emptyText, parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("📋 <b>Ваші завдання:</b>\n");
        
        var keyboardRows = new List<InlineKeyboardButton[]>();

        for (int i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            var statusIcon = t.IsCompleted ? "✅" : "⬜️";
            var title = t.IsCompleted ? $"<s>{t.Title}</s>" : t.Title;
            
            sb.AppendLine($"{i + 1}. {statusIcon} {title}");
            
            keyboardRows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(t.IsCompleted ? "🔄 Повернути" : "✅ Виконати", $"t_{t.Id}"),
                InlineKeyboardButton.WithCallbackData("🗑 Видалити", $"d_{t.Id}")
            });
        }

        var markup = new InlineKeyboardMarkup(keyboardRows);
        
        if (messageId.HasValue)
        {
            await botClient.EditMessageText(chatId, messageId.Value, sb.ToString(), parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: ct);
        }
        else
        {
            await botClient.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: ct);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram API Error occurred");
        return Task.CompletedTask;
    }
}