using System;

namespace WeatherChecker_DataCollectionJob
{
    #region WeatherChecker internal models
    public class Location
    {
        public Location(int zipCode, int accuWeatherLocationKey)
        {
            ZipCode = zipCode;
            AccuWeatherLocationKey = accuWeatherLocationKey;
        }

        public int ZipCode { get; private set; }
        public int AccuWeatherLocationKey { get; private set; }
    }

    public class WeatherData
    {
        public WeatherData(float high, float low, float precipitation, float cloudCover, float hoursOfPrecip, float windSpeed, float windDirection, float windGustSpeed)
        {
            HighTemp = high;
            LowTemp = low;
            PrecipitationAmount = precipitation;
            AverageCloudCover = cloudCover;
            NumHoursOfPrecipitation = hoursOfPrecip;
            WindSpeed = windSpeed;
            WindDirection = windDirection;
            WindGustSpeed = windGustSpeed;
        }

        public float HighTemp { get; private set; }
        public float LowTemp { get; private set; }
        public float PrecipitationAmount { get; private set; }
        public float AverageCloudCover { get; private set; }
        public float NumHoursOfPrecipitation { get; private set; }
        public float WindSpeed { get; private set; }
        public float WindDirection { get; private set; }
        public float WindGustSpeed { get; private set; }
    }

    #endregion

    #region AccuWeather Current Conditions

    public class WeatherObservation
    {
        public bool IsDayTime { get; set; }
        public int WeatherIcon { get; set; }
        public MeasurementSummary Temperature { get; set; }
        public TemperatureSummary TemperatureSummary { get; set; }
        public PrecipitationSummary PrecipitationSummary { get; set; }
        public WindObservationSummary Wind { get; set; }
        public WindObservationSummary WindGust { get; set; }
        public float CloudCover { get; set; }
    }

    public class PrecipitationSummary
    {
        public MeasurementSummary PastHour { get; set; }
        public MeasurementSummary Past24Hours { get; set; }
    }

    public class MeasurementSummary
    {
        public Measurement Imperial { get; set; }
    }

    public class Measurement
    {
        public float Value { get; set; }
    }

    public class TemperatureSummary
    {
        public TemperatureRange Past24HourRange { get; set; }
    }

    public class TemperatureRange
    {
        public MeasurementSummary Minimum { get; set; }
        public MeasurementSummary Maximum { get; set; }
    }

    public class WindObservationSummary
    {
        public MeasurementSummary Speed { get; set; }
        public WindDirection Direction { get; set; }
    }

    #endregion

    #region AccuWeather Weather forecast

    public class FiveDayForecast
    {
        public Forecast[] DailyForecasts { get; set; }
    }

    public class Forecast
    {
        public DateTime Date { get; set; }
        public ForecastTemperature Temperature { get; set; }
        public TimeOfDayForecast Day { get; set; }
        public TimeOfDayForecast Night { get; set; }

    }

    public class TimeOfDayForecast
    {
        public int Icon { get; set; }
        public Measurement TotalLiquid { get; set; }
        public Measurement Rain { get; set; }
        public Measurement Snow { get; set; }
        public Measurement Ice { get; set; }
        public float HoursOfPrecipitation { get; set; }
        public float HoursOfRain { get; set; }
        public float HoursOfSnow { get; set; }
        public float HoursOfIce { get; set; }
        public float CloudCover { get; set; }
        public WindForecastSummary Wind { get; set; }
        public WindForecastSummary WindGust { get; set; }
    }

    public class ForecastTemperature
    {
        public ForecastMeasurement Minimum { get; set; }
        public ForecastMeasurement Maximum { get; set; }
    }

    public class ForecastMeasurement
    {
        public float Value { get; set; }
    }

    public class WindForecastSummary
    {
        public ForecastMeasurement Speed { get; set; }
        public WindDirection Direction { get; set; }
    }

    #endregion

    #region AccuWeather shared classes

    public class WindDirection
    {
        public int Degrees { get; set; }
    }

    #endregion
}
