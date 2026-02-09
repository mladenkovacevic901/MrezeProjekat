using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Common
{
    // Enumeracije
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DeviceType
    {
        SolarniPanel,
        Vetrogenerator,
        Baterija,
        Potrosac
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PhysicalQuantity
    {
        C,  // Celsius
        K,  // Kelvin
        P,  // Power (Snaga)
        V,  // Voltage (Napon)
        A,  // Amperage (Struja)
        W   // Watt
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RequestType
    {
        Read,
        Write
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ResponseStatus
    {
        Success,
        Error,
        AlarmActive
    }

    // Konfiguracija uređaja koja se šalje putem UDP-a
    public class DeviceConfiguration
    {
        public int DeviceID { get; set; }
        public DeviceType Type { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public PhysicalQuantity Quantity { get; set; }
        public bool IsInput { get; set; }  // true = ulaz, false = izlaz
        public string IPAddress { get; set; } = string.Empty;
        public int Port { get; set; }

        public DeviceConfiguration() { }

        public DeviceConfiguration(int id, DeviceType type, double min, double max,
            PhysicalQuantity quantity, bool isInput, string ip, int port)
        {
            DeviceID = id;
            Type = type;
            MinValue = min;
            MaxValue = max;
            Quantity = quantity;
            IsInput = isInput;
            IPAddress = ip;
            Port = port;
        }

        public override string ToString()
        {
            return $"ID: {DeviceID}, Type: {Type}, Range: [{MinValue}-{MaxValue}] {Quantity}, " +
                   $"Direction: {(IsInput ? "Input" : "Output")}, Address: {IPAddress}:{Port}";
        }
    }

    // TCP zahtev od servera ka uređaju
    public class DeviceRequest
    {
        public RequestType Type { get; set; }
        public double? WriteValue { get; set; }  // Samo za Write zahteve

        public DeviceRequest() { }

        public DeviceRequest(RequestType type, double? writeValue = null)
        {
            Type = type;
            WriteValue = writeValue;
        }
    }

    // TCP odgovor od uređaja ka serveru
    public class DeviceResponse
    {
        public int DeviceID { get; set; }
        public ResponseStatus Status { get; set; }
        public double CurrentValue { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsAlarmActive { get; set; }

        public DeviceResponse() { }

        public DeviceResponse(int deviceId, ResponseStatus status, double value,
            string message = "", bool isAlarm = false)
        {
            DeviceID = deviceId;
            Status = status;
            CurrentValue = value;
            Message = message;
            IsAlarmActive = isAlarm;
        }
    }

    // Status uređaja na serveru
    public class DeviceStatus
    {
        public DeviceConfiguration Configuration { get; set; }
        public double LastValue { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool IsAlarmActive { get; set; }
        public string AlarmMessage { get; set; } = string.Empty;
        public bool IsConnected { get; set; }

        public DeviceStatus(DeviceConfiguration config)
        {
            Configuration = config;
            LastValue = 0;
            LastUpdate = DateTime.Now;
            IsAlarmActive = false;
            AlarmMessage = string.Empty;
            IsConnected = false;
        }

        public string GetStatusString()
        {
            string status = IsConnected ? "CONNECTED" : "DISCONNECTED";
            string alarm = IsAlarmActive ? " [ALARM!]" : "";
            return $"[{status}] ID:{Configuration.DeviceID} {Configuration.Type} | " +
                   $"Value: {LastValue:F2} {Configuration.Quantity} | " +
                   $"Last Update: {LastUpdate:HH:mm:ss}{alarm}";
        }
    }

    // Upit korisnika za stanje uređaja
    public class UserQuery
    {
        public int DeviceID { get; set; }

        public UserQuery(int deviceId)
        {
            DeviceID = deviceId;
        }
    }
}