using System.IO.Ports;

namespace AutoTf.GpsTest;

static class Program
{
	// Configuration required on chip:
	// Baudrate: 115200
	// Measurement period: 100ms
	
	
	private static double? _lastLatitude;
	private static double? _lastLongitude;
	private static double? _lastSpeed;
	private static bool _gpsValid;
	private static DateTime _lastTimestamp = DateTime.MinValue;
	
	static void Main()
	{
		string portName = "/dev/ttyACM0";
		int baudRate = 9600;

		SerialPort serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);

		try
		{
			Console.Clear();
			serialPort.Open();
			serialPort.DataReceived += DataReceivedHandler;
			Console.WriteLine("Listening for GPS data...");
			Console.ReadLine();
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error: " + ex.Message);
		}
		finally
		{
			serialPort.Close();
		}
	}

	private static void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
	{
		SerialPort sp = (SerialPort)sender;
		string line = sp.ReadLine();

		if (line.StartsWith("$GPGLL"))
		{
			Tuple<double, double>? coordinates = ParseNmea(line);
			
			if (coordinates != null)
			{
				UpdateDisplay(coordinates.Item1, coordinates.Item2, null);
			}
		}
		else if (line.StartsWith("$GPRMC"))
		{
			_gpsValid = ExtractGpsStatus(line);
			
			double speedKnots = ExtractSpeed(line);
			UpdateDisplay(null, null, speedKnots);
		}
	}
	
	private static bool ExtractGpsStatus(string nmea)
	{
		try
		{
			string[] parts = nmea.Split(',');

			if (parts.Length > 1)
			{
				string status = parts[2];
				return status == "A";
			}
		}
		catch
		{
			Console.WriteLine("Could not extract status");
		}

		return false; 
	}
	
	private static void UpdateDisplay(double? latitude, double? longitude, double? speed)
	{
		Console.SetCursorPosition(0, 1);

		if (!_gpsValid)
		{
			Console.WriteLine("No GPS signal...");
			return;
		}

		if (latitude != null && longitude != null)
		{
			_lastLatitude = latitude;
			_lastLongitude = longitude;
			
			DateTime now = DateTime.Now;
			
			if (_lastTimestamp != DateTime.MinValue)
			{
				double distance = CalculateDistance((double)_lastLatitude, (double)_lastLongitude, (double)latitude, (double)longitude);
				double timeElapsedSeconds = (now - _lastTimestamp).TotalSeconds;
				double calculatedSpeed = distance / timeElapsedSeconds;
				
				Console.SetCursorPosition(0, 1);
				Console.WriteLine($"Speed (calculated): {calculatedSpeed:F2} m/s, {calculatedSpeed * 3.6:F2} km/h");
			}
			
			_lastTimestamp = now;
		}

		if (speed != null)
		{
			double speedMps = (double)(speed * 0.514444); 
			if (speedMps < .25)
			{
				speedMps = 0;
				_lastSpeed = 0;
			}

			Console.SetCursorPosition(0, 2); 
			Console.WriteLine($"Speed (GPS): {_lastSpeed?.ToString("F2")} knots ({speedMps.ToString("F2")} m/s, {(_lastSpeed * 1.852)} km/h)");
		}

		Console.SetCursorPosition(0, 3);
		Console.WriteLine($"Latitude:  {_lastLatitude?.ToString("F6") ?? "N/A"}");
		Console.SetCursorPosition(0, 4); 
		Console.WriteLine($"Longitude: {_lastLongitude?.ToString("F6") ?? "N/A"}");
	}
	
	private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
	{
		double lat1Rad = ToRadians(lat1);
		double lon1Rad = ToRadians(lon1);
		double lat2Rad = ToRadians(lat2);
		double lon2Rad = ToRadians(lon2);

		double dlat = lat2Rad - lat1Rad;
		double dlon = lon2Rad - lon1Rad;

		double a = Math.Pow(Math.Sin(dlat / 2), 2) + Math.Cos(lat1Rad) * Math.Cos(lat2Rad) * Math.Pow(Math.Sin(dlon / 2), 2);
		double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

		double radiusOfEarth = 6371000;
		return radiusOfEarth * c;
	}

	private static double ToRadians(double degree)
	{
		return degree * Math.PI / 180.0;
	}
	
	private static double ExtractSpeed(string nmea)
	{
		try
		{
			string[] parts = nmea.Split(',');

			if (parts.Length > 7 && double.TryParse(parts[7], out double speedKnots))
			{
				return speedKnots;
			}
		}
		catch { }

		return 0;
	}

	private static Tuple<double, double>? ParseNmea(string nmea)
	{
		try
		{
			string[] parts = nmea.Split(',');

			if (parts.Length < 6 || string.IsNullOrEmpty(parts[1]) || string.IsNullOrEmpty(parts[3]))
				return null;

			double latitude = ConvertNmeaCoordinate(parts[1], parts[2]);
			double longitude = ConvertNmeaCoordinate(parts[3], parts[4]);

			return Tuple.Create(latitude, longitude);
		}
		catch
		{
			return null;
		}
	}

	private static double ConvertNmeaCoordinate(string value, string direction)
	{
		if (double.TryParse(value, out double coordinate))
		{
			double degrees = Math.Floor(coordinate / 100);
			double minutes = coordinate - (degrees * 100);
			double decimalDegrees = degrees + (minutes / 60);

			if (direction == "S" || direction == "W")
				decimalDegrees *= -1;

			return decimalDegrees;
		}
		return 0;
	}
}