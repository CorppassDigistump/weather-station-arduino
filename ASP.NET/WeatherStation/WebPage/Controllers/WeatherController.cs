﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Utility;
using Utility.Models;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebPage.Controllers
{
    public class WeatherController : Controller
    {
        private readonly WeatherContext _context;

        public WeatherController(WeatherContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Select Latest Data
            ViewData["LatestData"] = _context.Weathers.OrderByDescending(x => x.DateTime)
                .First();

            #region Select 6 Hour Data
            List<Weather> hoursData = new List<Weather>();

            var groups = _context.Weathers.GroupBy(x => x.DateTime.Date)
                .OrderByDescending(x => x.Key)
                .Take(2)
                .Select(g => g.Select(x => x).ToList()).ToList();

            var today = groups[0].GroupBy(x => x.DateTime.Hour)
                .OrderByDescending(x => x.Key)
                .Select(g => g.Select(x => x).ToList()).ToList();

            int count = today.Count;
            if (count >= 6)
            {
                var temp = today.Take(6);

                foreach (var item in temp)
                {
                    hoursData.Add(item[0]);
                }
            }
            else
            {
                var yesterday = groups[1].GroupBy(x => x.DateTime.Hour)
                    .OrderByDescending(x => x.Key)
                    .Select(g => g.Select(x => x).ToList()).ToList();

                var temp = yesterday.Take(6 - count);

                foreach (var item in today)
                {
                    hoursData.Add(item[0]);
                }

                foreach (var item in temp)
                {
                    hoursData.Add(item[0]);
                }
            }

            hoursData.Reverse();

            ViewData["HoursData"] = hoursData;
            #endregion

            return View();
        }
    }
}
