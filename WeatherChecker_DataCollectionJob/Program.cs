using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;

namespace WeatherChecker_DataCollectionJob
{
    class Program
    {
        // Please set the following connection strings in app.config for this WebJob to run:
        // AzureWebJobsDashboard and AzureWebJobsStorage
        static void Main()
        {
            var config = new JobHostConfiguration();

            if (config.IsDevelopment)
            {
                config.UseDevelopmentSettings();
            }

            // Connect to database
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            builder.DataSource = Environment.GetEnvironmentVariable(Constants.Database.DatabaseConnectionEnvironmentVariable);
            builder.InitialCatalog = Environment.GetEnvironmentVariable(Constants.Database.DatabaseNameEnvironmentVariable);

            #if DEBUG
                // The local SQL server instance for testing is set up to use Windows Authentication
                builder.IntegratedSecurity = true;
            #else
                builder.UserID = Environment.GetEnvironmentVariable(Constants.Database.UserIDEnvironmentVariable);
                builder.Password = Environment.GetEnvironmentVariable(Constants.Database.PasswordEnvironmentVariable);
            #endif

            SqlConnection dbConnection = new SqlConnection(builder.ConnectionString);
            dbConnection.Open();

            var host = new JobHost(config);
            host.Start();

            Task dataTask = GetLatestDataAsync(dbConnection).ContinueWith(innerTask =>
            {
                if (innerTask.IsFaulted)
                {
                    Debug.WriteLine($"GetLatestData failed with expection {innerTask.Exception}");
                }

                dbConnection.Close();
                dbConnection.Dispose();
            });

            dataTask.Wait();
            host.Stop();
        }

        static Task GetLatestDataAsync(SqlConnection dbConnection)
        {
            List<Task> locationTasks = new List<Task>();
            foreach (Location location in Constants.AccuWeather.Locations)
            {
                locationTasks.Add(GetLocationData(location, dbConnection));
            }

            return Task.WhenAll(locationTasks);
        }

        static Task GetLocationData(Location location, SqlConnection dbConnection)
        {
            return Task.WhenAll(new List<Task>()
            {
                GetPast24HrData(location, dbConnection),
                GetFiveDayForecastData(location, dbConnection)
            });
        }

        static async Task GetPast24HrData(Location location, SqlConnection dbConnection)
        {
            string accuWeatherAPIKey = Environment.GetEnvironmentVariable(Constants.AccuWeather.APIKeyEnvironmentVariable);

            string currentConditionsUrl = string.Format(Constants.AccuWeather.CurrentConditionsURLFormat, location.AccuWeatherLocationKey, accuWeatherAPIKey);
            Task<WebResponse> currentConditionsResponseTask = WebRequest.CreateHttp(currentConditionsUrl).GetResponseAsync();

            WebResponse currentConditionsResponse = await currentConditionsResponseTask;
            using (StreamReader stream = new StreamReader(currentConditionsResponse.GetResponseStream()))
            {
                string jsonText = stream.ReadToEnd();
                WeatherObservation[] observations = JsonConvert.DeserializeObject<WeatherObservation[]>(jsonText);
                Debug.Assert(observations.Length > 0);

                // Get the first observation and use it to get the high and low from the previous 24 hours
                WeatherObservation firstObservation = observations[0];
                int highTemp = (int)firstObservation.TemperatureSummary.Past24HourRange.Maximum.Imperial.Value;
                int lowTemp = (int)firstObservation.TemperatureSummary.Past24HourRange.Minimum.Imperial.Value;

                WeatherData daytimeData = GetWeatherData(observations, true, highTemp, lowTemp);
                WeatherData nighttimeData = GetWeatherData(observations, false, highTemp, lowTemp);
                
                // Setting DaysBefore = -1 indicates that this is the actual observation data for the target date
                InsertWeatherData(dbConnection, location.ZipCode, NormalizeDate(DateTime.Now), -1, daytimeData, nighttimeData);
            }
        }

        static async Task GetFiveDayForecastData(Location location, SqlConnection dbConnection)
        {
            string accuWeatherAPIKey = Environment.GetEnvironmentVariable(Constants.AccuWeather.APIKeyEnvironmentVariable);

            string forecastUrl = string.Format(Constants.AccuWeather.FiveDayForecastURLFormat, location.AccuWeatherLocationKey, accuWeatherAPIKey);
            Task<WebResponse> forecastResponseTask = WebRequest.CreateHttp(forecastUrl).GetResponseAsync();

            WebResponse forecastResponse = await forecastResponseTask;
            using (StreamReader stream = new StreamReader(forecastResponse.GetResponseStream()))
            {
                string jsonText = stream.ReadToEnd();
                FiveDayForecast fiveDayForecast = JsonConvert.DeserializeObject<FiveDayForecast>(jsonText);

                int daysBefore = 0;
                foreach (Forecast forecast in fiveDayForecast.DailyForecasts)
                {
                    WeatherData dayData = new WeatherData(forecast.Temperature.Maximum.Value,
                        forecast.Temperature.Minimum.Value,
                        forecast.Day.Rain.Value + forecast.Day.Snow.Value + forecast.Day.Ice.Value,
                        forecast.Day.CloudCover,
                        (int)Math.Round((double)forecast.Day.HoursOfPrecipitation, 0),
                        forecast.Day.Wind.Speed.Value,
                        forecast.Day.Wind.Direction.Degrees,
                        forecast.Day.WindGust.Speed.Value);

                    WeatherData nightData = new WeatherData(forecast.Temperature.Maximum.Value,
                        forecast.Temperature.Minimum.Value,
                        forecast.Night.Rain.Value + forecast.Night.Snow.Value + forecast.Night.Ice.Value,
                        forecast.Night.CloudCover,
                        (int)Math.Round((double)forecast.Night.HoursOfPrecipitation, 0),
                        forecast.Night.Wind.Speed.Value,
                        forecast.Night.Wind.Direction.Degrees,
                        forecast.Night.WindGust.Speed.Value);

                    InsertWeatherData(dbConnection, location.ZipCode, NormalizeDate(forecast.Date), daysBefore, dayData, nightData);
                    daysBefore++;
                }
            }
        }

        static void InsertWeatherData(SqlConnection dbConnection, int locationZipCode, DateTime targetDate, int numDaysBeforeTarget, WeatherData daytimeData, WeatherData nightData)
        {
            try
            {
                DateTime dataDate = NormalizeDate(DateTime.Now);

                string queryStringFormat = "INSERT INTO dbo.WeatherData (zipCode, targetDate, daysBefore, high, low, precipitationAmount, avgCloudCover, hoursOfPrecipitation, windSpeed, windDirection, windGustSpeed, isDayTime) " +
                    "VALUES ({0}, '{1}', {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, '{11}')";

                string daytimeDataQuery = string.Format(queryStringFormat,
                    locationZipCode,
                    targetDate,
                    numDaysBeforeTarget,
                    daytimeData.HighTemp,
                    daytimeData.LowTemp,
                    daytimeData.PrecipitationAmount,
                    daytimeData.AverageCloudCover,
                    daytimeData.NumHoursOfPrecipitation,
                    daytimeData.WindSpeed,
                    daytimeData.WindDirection,
                    daytimeData.WindGustSpeed,
                    true.ToString().ToUpper());
                SqlCommand daytimeCommand = new SqlCommand(daytimeDataQuery, dbConnection);
                int numRowsAffected = daytimeCommand.ExecuteNonQuery();
                Debug.Assert(numRowsAffected == 1);

                string nightDataQuery = string.Format(queryStringFormat,
                    locationZipCode,
                    targetDate,
                    numDaysBeforeTarget,
                    nightData.HighTemp,
                    nightData.LowTemp,
                    nightData.PrecipitationAmount,
                    nightData.AverageCloudCover,
                    nightData.NumHoursOfPrecipitation,
                    nightData.WindSpeed,
                    nightData.WindDirection,
                    nightData.WindGustSpeed,
                    false.ToString().ToUpper());
                SqlCommand nightCommand = new SqlCommand(nightDataQuery, dbConnection);
                numRowsAffected = nightCommand.ExecuteNonQuery();
                Debug.Assert(numRowsAffected == 1);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to insert weather data with error: \n\t{e}");
            }
        }

        static DateTime NormalizeDate(DateTime date)
        {
            // The script will run around 5am, gathering data from the preceeding day.
            // If the date is from the morning (before noon), we'll save the data as for the day before.
            DateTime normalizedDate = new DateTime(date.Year, date.Month, date.Day);

            if (date.Hour < 12)
            {
                // Subtract one day
                normalizedDate = normalizedDate.Subtract(new TimeSpan(1, 0, 0, 0));
            }

            return normalizedDate;
        }

        static WeatherData GetWeatherData(WeatherObservation[] observations, bool getDaytimeData, int highTemp, int lowTemp)
        {
            float precipitation = 0;

            int numMatchingHours = 0;
            int numPrecipHours = 0;
            float windGustMax = 0;

            // These are sums of the values for all hours, which we'll use to get an average across all hours.
            float windSpeedTotal = 0;
            float windDirectionTotal = 0;
            float cloudCoverTotal = 0;
            foreach (WeatherObservation observation in observations)
            {
                if (observation.IsDayTime == getDaytimeData)
                {
                    numMatchingHours++;

                    if (observation.PrecipitationSummary.PastHour.Imperial.Value > 0)
                    {
                        numPrecipHours++;
                    }

                    if (observation.WindGust.Speed.Imperial.Value > windGustMax)
                    {
                        windGustMax = observation.WindGust.Speed.Imperial.Value;
                    }

                    precipitation += observation.PrecipitationSummary.PastHour.Imperial.Value;

                    windSpeedTotal += observation.Wind.Speed.Imperial.Value;
                    windDirectionTotal += observation.Wind.Direction.Degrees;
                    cloudCoverTotal += observation.CloudCover;
                }
            }

            float windSpeedAvg = windSpeedTotal / numMatchingHours;
            float windDirectionAvg = windDirectionTotal / numMatchingHours;
            float cloudCoverAverage = cloudCoverTotal / numMatchingHours;

            return new WeatherData(highTemp, lowTemp, precipitation, cloudCoverAverage, numPrecipHours, windSpeedAvg, windDirectionAvg, windGustMax);
        }
    }
}
