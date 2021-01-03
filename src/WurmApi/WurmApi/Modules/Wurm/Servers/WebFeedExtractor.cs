using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AldursLab.WurmApi.Utility;

namespace AldursLab.WurmApi.Modules.Wurm.Servers
{
    class WebFeedExtractor
    {
        readonly IHttpWebRequests httpWebRequests;

        public WebFeedExtractor(IHttpWebRequests httpWebRequests)
        {
            if (httpWebRequests == null)
            {
                throw new ArgumentNullException(nameof(httpWebRequests));
            }
            this.httpWebRequests = httpWebRequests;
        }

        public async Task<WebDataExtractionResult> ExtractAsync(WurmServerInfo serverInfo)
        {
            WebDataExtractionResult result = new WebDataExtractionResult(serverInfo.ServerName);

            var res = await httpWebRequests.GetResponseAsync(serverInfo.WebStatsUrl).ConfigureAwait(false);
            DateTime headerLastUpdated = res.LastModified;

            using (Stream stream = res.GetResponseStream())
            {
                using (var reader = new StreamReader(stream))
                {
                    bool canReadServerName = false;
                    bool canReadUptime = false;
                    bool canReadWurmTime = false;
                    string line;
                    string strUptime = string.Empty;
                    string strWurmDateTime = string.Empty;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (canReadServerName)
                        {
                            Match match = Regex.Match(line, @">.+<");
                            string name = match.Value.Substring(1, match.Value.Length - 2);
                            if (!string.Equals(name, serverInfo.ServerName.Original, StringComparison.InvariantCultureIgnoreCase))
                            {
                                throw new WurmApiException(
                                    string.Format(
                                        "Extracted server name does not match server description, expected {0}, actual {1}",
                                        serverInfo.ServerName.Original,
                                        name));
                            }
                            canReadServerName = false;
                        }
                        else if (canReadUptime)
                        {
                            Match match = Regex.Match(line, @">.+<");
                            strUptime = match.Value.Substring(1, match.Value.Length - 2);
                            canReadUptime = false;
                        }
                        else if (canReadWurmTime)
                        {
                            Match match = Regex.Match(line, @">.+<");
                            strWurmDateTime = match.Value.Substring(1, match.Value.Length - 2);
                            canReadWurmTime = false;
                        }

                        if (Regex.IsMatch(line, "Server name"))
                        {
                            canReadServerName = true;
                        }
                        else if (Regex.IsMatch(line, "Uptime"))
                        {
                            canReadUptime = true;
                        }
                        else if (Regex.IsMatch(line, "Wurm Time"))
                        {
                            canReadWurmTime = true;
                        }
                    }

                    DateTime dtnow = Time.Get.LocalNow;
                    if (headerLastUpdated > dtnow)
                    {
                        headerLastUpdated = dtnow;
                    }

                    TimeSpan uptime = GetTimeSpanFromUptimeWebString(strUptime);
                    result.ServerUptime = uptime;

                    WurmDateTime wdt = GetWurmDateTimeFromWdtWebString(strWurmDateTime);
                    result.WurmDateTime = wdt;

                    result.LastUpdated = headerLastUpdated;
                }
            }

            return result;
        }

        public WebDataExtractionResult Extract(WurmServerInfo serverInfo)
        {
            return TaskHelper.UnwrapSingularAggegateException(() => ExtractAsync(serverInfo).Result);
        }

        static TimeSpan GetTimeSpanFromUptimeWebString(string webString)
        {
            //ex: The server has been up 1 days, 14 hours and 43 minutes.
            //ex: The server has been up 1 day, 1 hour and 1 minute.
            //note: non capturing group for "s" is redundant, added for clarity.
            Match matchdays = Regex.Match(webString, @"\d\d* day(?:s)");
            Match matchhours = Regex.Match(webString, @"\d\d* hour(?:s)");
            Match matchminutes = Regex.Match(webString, @"\d\d* minute(?:s)");
            Match matchseconds = Regex.Match(webString, @"\d\d* second(?:s)");

            int days = 0;
            int hours = 0;
            int minutes = 0;
            int seconds = 0;

            int successCount = 0;

            if (matchdays.Success)
            {
                successCount++;
                days = MatchToInt32(matchdays);
            }
            if (matchhours.Success)
            {
                successCount++;
                hours = MatchToInt32(matchhours);
            }
            if (matchminutes.Success)
            {
                successCount++;
                minutes = MatchToInt32(matchminutes);
            }
            if (matchseconds.Success)
            {
                successCount++;
                seconds = MatchToInt32(matchseconds);
            }

            if (successCount == 0)
            {
                throw new WurmApiException(string.Format("Parsed nothing out of uptime string: {0}", webString));
            }

            return new TimeSpan(days, hours, minutes, seconds);
        }

        public static int MatchToInt32(Match match)
        {
            return Convert.ToInt32(Regex.Match(match.ToString(), @"\d\d*").ToString());
        }

        static WurmDateTime GetWurmDateTimeFromWdtWebString(string logline)
        {
            //time
            Match wurmTime = Regex.Match(logline, @" \d\d:\d\d:\d\d ");
            int hour = Convert.ToInt32(wurmTime.Value.Substring(1, 2));
            int minute = Convert.ToInt32(wurmTime.Value.Substring(4, 2));
            int second = Convert.ToInt32(wurmTime.Value.Substring(7, 2));
            //day
            WurmDay? day = null;
            foreach (string name in WurmDay.AllNormalizedNames)
            {
                if (Regex.IsMatch(logline, name, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                {
                    day = new WurmDay(name);
                    break;
                }
            }
            //week
            Match wurmWeek = Regex.Match(logline, @"week (\d)");
            int week = Convert.ToInt32(wurmWeek.Groups[1].Value);
            //month(starfall)
            WurmStarfall? starfall = null;
            foreach (string name in WurmStarfall.AllNormalizedNames)
            {
                if (Regex.IsMatch(logline, name, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                {
                    starfall = new WurmStarfall(name);
                    break;
                }
            }
            //year
            Match wurmYear = Regex.Match(logline, @"in the year of (\d+)");
            int year = Convert.ToInt32(wurmYear.Groups[1].Value);

            if (day == null || starfall == null)
                throw new Exception("log line was not parsed correctly into day or starfall: " + (logline ?? "NULL"));

            return new WurmDateTime(year, starfall.Value, week, day.Value, hour, minute, second);
        }
    }

    public class WebDataExtractionResult
    {
        public ServerName ServerName { get; private set; }

        public WebDataExtractionResult(ServerName serverName)
        {
            ServerName = serverName;
        }

        public WurmDateTime WurmDateTime { get; set; }
        public TimeSpan ServerUptime { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
    }
}