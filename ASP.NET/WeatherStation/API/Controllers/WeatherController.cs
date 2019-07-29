﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using SkiaSharp;
using Utility;
using Utility.Models;
using ControllerBase = Microsoft.AspNetCore.Mvc.ControllerBase;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WeatherController : ControllerBase
    {
        private readonly WeatherContext _context;

        public WeatherController(WeatherContext context)
        {
            _context = context;
        }

        [HttpGet]
        public Weather Get(int id = 1)
        {
            try
            {
                return _context.Weathers.Where(x => x.WeatherID == id).First();
            }
            catch (Exception)
            {
                return null;
            }
        }

        [HttpPost]
        public async Task<string> Post([FromQuery]string token, [FromQuery]Weather weather)
        {
            if (token != "xxxxx")
            {
                return "Token error.";
            }
            
            try
            {
                weather.DateTime = DateTime.Now;
                weather.WeatherName = await GetXinzhiWeather();

                await PostWeibo(weather);

                _context.Weathers.Add(weather);
                await _context.SaveChangesAsync();

                return "True";
            }
            catch (Exception)
            {
                return "False";
            }
        }

        /// <summary>
        /// Calculate Heat Index
        /// </summary>
        /// <param name="temp">Temperature in celsius</param>
        /// <param name="rh">Humidity in percent, like 56(56%)</param>
        /// <returns>Temperature in celsius</returns>
        private double CalHeatIndex(double temp, double rh)
        {
            double t = temp * 1.8 + 32;
            double hi;

            hi = -42.379 + 2.04901523 * t + 10.14333127 * rh - .22475541 * t * rh
             -.00683783 * t * t - .05481717 * rh * rh + .00122874 * t * t * rh
             +.00085282 * t * rh * rh - .00000199 * t * t * rh * rh;

            if (hi < 80)
                hi = 0.5 * t + 61.0 + (t - 68.0) * 1.2 + rh * 0.094;
            else
                if (rh < 13 && (t > 80 || t < 112))
                    hi -= (13 - rh) / 4.0 * Math.Sqrt((17 - Math.Abs(t - 95)) / 17.0);
                else if (rh > 85 && (t > 80 || t < 87))
                    hi += (rh - 85) * (87 - t) / 50.0;

            return (hi - 32) / 1.8;
        }

        private async Task<string> GetXinzhiWeather()
        {
            string key = "";  // xinzhi key
            string location = "";  // like 35.66:123.55

            using (HttpClient client=new HttpClient())
            {
                var response = await client.GetAsync($"https://api.seniverse.com/v3/weather/now.json?key={key}&location={location}&language=zh-Hans&unit=c");
                string json = await response.Content.ReadAsStringAsync();

                return (string)JsonConvert.DeserializeObject<dynamic>(json).results[0].now.text;
            }
        }

        private async Task PostWeibo(Weather weather)
        {
            string token = "";  // weibo token
            string status = $"{weather.DateTime.ToString("yyyy/MM/dd HH:mm")}    {weather.WeatherName}%0a" +
                    $"温度：{Math.Round(weather.Temperature, 1)} ℃    体感温度：{Math.Round(CalHeatIndex(weather.Temperature, weather.Humidity), 1)} ℃%0a" +
                    $"相对湿度：{Math.Round(weather.Humidity)} %25%0a" +
                    $"气压：{Math.Round(weather.Pressure / 100, 2)} hPa%0a" +
                    //$"可吸入颗粒物：{weather.Dust} mg%2fm3%0a" +
                    $"http://maestrobot.cn";

            using (HttpClient client = new HttpClient())
            {
                using (HttpContent content = new StringContent(""))
                {
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

                    HttpResponseMessage response = await client.PostAsync($"https://api.weibo.com/2/statuses/share.json?access_token={token}&status={status}", content);
                }
            }

            //var hoursData = _context.Select6HourData();

            //using (HttpClient client = new HttpClient())
            //{
            //    Svg2Png(GetTempModel(hoursData));
            //    var pngStream = System.IO.File.OpenRead("img.png");

            //    MultipartFormDataContent content = new MultipartFormDataContent
            //    {
            //        { new StringContent(token, Encoding.UTF8), "access_token" },
            //        { new StringContent(status, Encoding.UTF8), "status" },
            //        { new StreamContent(pngStream, (int)pngStream.Length), "pic", "img.png" }
            //    };

            //    HttpResponseMessage response = await client.PostAsync("https://api.weibo.com/2/statuses/share.json", content);
            //    var  s = await content.ReadAsStringAsync();
            //    var str = await response.Content.ReadAsStringAsync();

            //    pngStream.Dispose();
            //}
        }

        private PlotModel GetTempModel(List<Weather> hoursData)
        {
            DateTimeAxis dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = DateTimeAxis.ToDouble(hoursData[0].DateTime),
                Maximum = DateTimeAxis.ToDouble(hoursData[5].DateTime),
                StringFormat = "MM/dd hh:mm"
            };

            PlotModel tempModel = new PlotModel { Title = "环境温度" };
            tempModel.Axes.Add(
                new LinearAxis
                {
                    Title = "温度（℃）",
                    Position = AxisPosition.Left,
                    Minimum = hoursData.Min(x => x.Temperature) - 2,
                    Maximum = hoursData.Max(x => x.Temperature) + 2
                });
            tempModel.Axes.Add(dateAxis);

            tempModel.Background = OxyColor.FromRgb(255, 255, 255);

            var tempSeries = new LineSeries
            {
                Color = OxyColor.FromRgb(194, 53, 49),
                MarkerFill = OxyColor.FromRgb(0, 0, 0),
                MarkerType = MarkerType.Circle
            };

            foreach (var item in hoursData)
            {
                tempSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(item.DateTime), item.Temperature));
            }

            tempModel.Series.Add(tempSeries);

            return tempModel;
        }

        private PlotModel GetHumiModel(DateTimeAxis dateAxis, List<Weather> hoursData)
        {
            PlotModel model = new PlotModel { Title = "相对湿度" };
            model.Axes.Add(
                new LinearAxis
                {
                    Title = "湿度（%）",
                    Position = AxisPosition.Left,
                    Minimum = hoursData.Min(x => x.Humidity) - 2,
                    Maximum = hoursData.Max(x => x.Humidity) + 2
                });
            model.Axes.Add(dateAxis);

            var series = new LineSeries
            {
                Color = OxyColor.FromRgb(47, 69, 84),
                MarkerFill = OxyColor.FromRgb(0, 0, 0),
                MarkerType = MarkerType.Circle
            };

            foreach (var item in hoursData)
            {
                series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(item.DateTime), item.Humidity));
            }

            model.Series.Add(series);

            return model;
        }

        private PlotModel GetPressModel(DateTimeAxis dateAxis, List<Weather> hoursData)
        {
            PlotModel model = new PlotModel { Title = "气压" };
            model.Axes.Add(
                new LinearAxis
                {
                    Title = "气压（Pa）",
                    Position = AxisPosition.Left,
                    Minimum = hoursData.Min(x => x.Pressure) - 50,
                    Maximum = hoursData.Max(x => x.Pressure) + 50
                });
            model.Axes.Add(dateAxis);

            var series = new LineSeries
            {
                Color = OxyColor.FromRgb(97, 160, 168),
                MarkerFill = OxyColor.FromRgb(0, 0, 0),
                MarkerType = MarkerType.Circle
            };

            foreach (var item in hoursData)
            {
                series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(item.DateTime), item.Pressure));
            }

            model.Series.Add(series);

            return model;
        }

        private PlotModel GetDustModel(DateTimeAxis dateAxis, List<Weather> hoursData)
        {
            PlotModel model = new PlotModel { Title = "可吸入颗粒物" };
            model.Axes.Add(
                new LinearAxis
                {
                    Title = "可吸入颗粒物（mg/m³）",
                    Position = AxisPosition.Left,
                    Minimum = hoursData.Min(x => x.Dust) - 0.02,
                    Maximum = hoursData.Max(x => x.Dust) + 0.02
                });
            model.Axes.Add(dateAxis);

            var series = new LineSeries
            {
                Color = OxyColor.FromRgb(212, 130, 101),
                MarkerFill = OxyColor.FromRgb(0, 0, 0),
                MarkerType = MarkerType.Circle
            };

            foreach (var item in hoursData)
            {
                series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(item.DateTime), item.Dust));
            }

            model.Series.Add(series);

            return model;
        }

        private void Svg2Png(PlotModel model)
        {
            SvgExporter exporter = new SvgExporter { Width = 600, Height = 400 };
            var svg = new SkiaSharp.Extended.Svg.SKSvg();
            var imgQuality = 80;

            using (FileStream fs = System.IO.File.Create("temp.svg"))
            {
                exporter.Export(model, fs);
            }

            using (FileStream png = System.IO.File.Create("img.png"))
            {
                var pict = svg.Load("temp.svg");

                var dimen = new SKSizeI(
                    (int)Math.Ceiling(pict.CullRect.Width),
                    (int)Math.Ceiling(pict.CullRect.Height)
                );
                var matrix = SKMatrix.MakeScale(1, 1);
                var img = SKImage.FromPicture(pict, dimen, matrix);

                // convert to PNG
                var skdata = img.Encode(SKEncodedImageFormat.Png, imgQuality);

                skdata.SaveTo(png);
            }
        }
    }
}