using System.IO.Ports;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerPilot.Core.Interfaces;
using PowerPilot.Core.Models;

namespace PowerPilot.P1Reader;

public class P1ReaderOptions
{
    public string SerialPort { get; set; } = "/dev/ttyUSB0";
    public int BaudRate { get; set; } = 115200;
}

public class SerialP1Reader : IP1Reader, IDisposable
{
    private readonly ILogger<SerialP1Reader> _logger;
    private readonly P1ReaderOptions _options;
    private SerialPort? _serialPort;
    private readonly StringBuilder _telegramBuffer = new();
    private bool _inTelegram;
    private CancellationTokenSource? _cts;

    public event EventHandler<P1Telegram>? TelegramReceived;
    public bool IsConnected => _serialPort?.IsOpen ?? false;

    public SerialP1Reader(ILogger<SerialP1Reader> logger, IOptions<P1ReaderOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            _serialPort = new SerialPort(_options.SerialPort, _options.BaudRate, Parity.None, 8, StopBits.One);
            _serialPort.DataReceived += OnDataReceived;
            _serialPort.Open();
            _logger.LogInformation("P1 reader started on {Port} at {Baud} baud", _options.SerialPort, _options.BaudRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open serial port {Port}", _options.SerialPort);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        if (_serialPort?.IsOpen == true)
        {
            _serialPort.DataReceived -= OnDataReceived;
            _serialPort.Close();
        }
        _logger.LogInformation("P1 reader stopped");
        return Task.CompletedTask;
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var data = _serialPort!.ReadExisting();
            foreach (var ch in data)
            {
                if (ch == '/') { _inTelegram = true; _telegramBuffer.Clear(); }
                if (_inTelegram)
                {
                    _telegramBuffer.Append(ch);
                    if (ch == '!' && _telegramBuffer.Length > 5)
                    {
                        ProcessTelegram(_telegramBuffer.ToString());
                        _telegramBuffer.Clear();
                        _inTelegram = false;
                    }
                }
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error reading from serial port"); }
    }

    private void ProcessTelegram(string raw)
    {
        var telegram = DsmrP1Parser.Parse(raw);
        if (telegram != null)
        {
            _logger.LogDebug("Telegram received: Power={Power}kW", telegram.CurrentPowerUsage - telegram.CurrentPowerDelivery);
            TelegramReceived?.Invoke(this, telegram);
        }
    }

    public void Dispose() { _serialPort?.Dispose(); _cts?.Dispose(); }
}
