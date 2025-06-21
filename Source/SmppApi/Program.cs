using BSN.SmppClient;
using BSN.SmppClient.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;

namespace SmppApi
{
    public class Program
    {
        private static ESMEManager _connectionManager;

        private static ILogger _logger;

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddAuthorization();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddLogging(opts =>
            {
                opts.AddConsole();
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();

            _logger = app.Services.GetRequiredService<ILogger<Program>>();

            _logger.LogInformation("Logger intialized.");

            InitializeSmppConnection(builder.Configuration);

            app.MapPost("/send", async ([FromBody] SendMessageRequest request) =>
            {
                try
                {
                    var result = SendMessage(request.PhoneNumber, request.Message, builder.Configuration);
                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { Error = ex.Message });
                }
            })
            .WithName("SendMessage")
            .WithOpenApi();

            app.MapGet("/querymessage/{messageId}", async (string messageId) =>
            {
                try
                {
                    var result = QueryMessage(messageId);
                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { Error = ex.Message });
                }
            })
            .WithName("QueryMessage")
            .WithOpenApi();

            app.Run();
        }

        private static void InitializeSmppConnection(IConfiguration configuration)
        {
            string server = configuration["Smpp:Server"];
            short port = short.Parse(configuration["Smpp:Port"]);
            string shortLongCode = configuration["Smpp:ShortLongCode"];
            string systemId = configuration["Smpp:SystemId"];
            string password = configuration["Smpp:Password"];
            DataCodings dataCoding = Enum.Parse<DataCodings>(configuration["Smpp:DataCoding"]);

            _connectionManager = new ESMEManager("Test",
                shortLongCode,
                ConnectionEventHandler,
                ReceivedMessageHandler,
                ReceivedGenericNackHandler,
                SubmitMessageHandler,
                QueryMessageHandler,
                LogEventHandler,
                PduDetailsHandler);

            _connectionManager.AddConnections(1, ConnectionModes.Transceiver, server, port, systemId, password, "Transceiver", dataCoding);
        }

        private static object SendMessage(string phoneNumber, string message, IConfiguration configuration)
        {
            _logger.LogInformation($"Sending message to {phoneNumber}");
            int result = 0;
            try
            {
                DataCodings submitDataCoding = Enum.Parse<DataCodings>(configuration["Smpp:SubmitDataCoding"]);
                DataCodings encodeDataCoding = Enum.Parse<DataCodings>(configuration["Smpp:EncodeDataCoding"]);
                Ton srcTon = Enum.Parse<Ton>(configuration["Smpp:SrcTon"]);
                Npi srcNpi = Enum.Parse<Npi>(configuration["Smpp:SrcNpi"]);
                Ton dstTon = Enum.Parse<Ton>(configuration["Smpp:DstTon"]);
                Npi dstNpi = Enum.Parse<Npi>(configuration["Smpp:DstNpi"]);

                result = _connectionManager.SendMessage(phoneNumber, null, srcTon, srcNpi, dstTon, dstNpi, submitDataCoding, encodeDataCoding, message, out SubmitSm submitSm, out SubmitSmResp submitSmResp);
                _logger.LogInformation($"Send message Result: {result}");

                return new { SubmitSm = submitSm.DestAddr, SubmitSmResp = submitSmResp.Status, MessageId = submitSmResp.MessageId };
            }
            catch (Exception)
            {
                throw new Exception($"Connection not established correctly.");
            }
        }

        private static object QueryMessage(string messageId)
        {
            QuerySm querySm = _connectionManager.SendQuery(messageId);
            return new { Status = querySm.Status.ToString() };
        }

        private static void ReceivedMessageHandler(string logKey, MessageTypes messageType, string serviceType, Ton sourceTon, Npi sourceNpi, string shortLongCode, DateTime dateReceived, string phoneNumber, DataCodings dataCoding, string message)
        {
            _logger.LogInformation($"logKey: {logKey}, messageType: {messageType.ToString()}, message: {message}");
        }

        private static void ReceivedGenericNackHandler(string logKey, int sequence)
        {
            _logger.LogInformation($"logKey: {logKey}, sequence: {sequence.ToString()}");
        }

        private static void SubmitMessageHandler(string logKey, int sequence, string messageId)
        {
            _logger.LogInformation($"logKey: {logKey}, sequence: {sequence.ToString()}, messageId: {messageId}");

        }

        private static void QueryMessageHandler(string logKey, int sequence, string messageId, DateTime finalDate, int messageState, long errorCode)
        {
            _logger.LogInformation($"logKey: {logKey}, sequence: {sequence.ToString()}, messageId: {messageId}");
        }

        private static void LogEventHandler(LogEventNotificationTypes logEventNotificationType, string logKey, string shortLongCode, string message)
        {
            _logger.LogInformation($"logKey: {logKey}, shortLongCode: {shortLongCode}, message: {message}");
        }

        private static void ConnectionEventHandler(string logKey, ConnectionEventTypes connectionEventType, string message)
        {
            _logger.LogInformation($"logKey: {logKey}, shortLongCode: {connectionEventType.ToString()}, message: {message}");
        }

        private static Guid? PduDetailsHandler(string logKey, PduDirectionTypes pduDirectionType, Header pdu, List<PduPropertyDetail> details)
        {
            if ((pdu.Command == CommandSet.EnquireLink) || (pdu.Command == CommandSet.EnquireLinkResp))
            {
                return null;
            }

            string? connectionString = null;
            int serviceId = 0;
            Guid? pduHeaderId = null;

            try
            {
                PduApp.InsertPdu(logKey, connectionString, serviceId, pduDirectionType, details, pdu.PduData.BreakIntoDataBlocks(4096), out pduHeaderId);
            }
            catch (Exception exception)
            {
            }

            return pduHeaderId;
        }
    }

    public class SendMessageRequest
    {
        public string PhoneNumber { get; set; }
        public string Message { get; set; }
    }
}