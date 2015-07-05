using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text.RegularExpressions;


public partial class _Default : System.Web.UI.Page {

    string pom;
    protected void Page_Load(object sender, EventArgs e) {

        //
        // Page call: http://xxx.domain.net/?pom=Büro&temp=14&hum=14
        //

        try {
            TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
            DateTime dt = TimeZoneInfo.ConvertTime(DateTime.Now,tzi);

            pom = HttpContext.Current.Request["pom"];
            if (HttpContext.Current.Request.QueryString.AllKeys.Contains("delete")){
                DeleteOldData();
                Response.Write("Old data deleted");
                Response.Flush();
                Response.SuppressContent = true;
                ApplicationInstance.CompleteRequest(); 
                return;
            }

            string hum = HttpContext.Current.Request["hum"];
            string temp = HttpContext.Current.Request["temp"];
            string dateTime = dt.ToString("yyyy-MM-dd HH:mm:ss");
            string restDate = dt.ToString("yyyyMMdd");
            string restTime = "0000";

            int min = Convert.ToInt32(dt.ToString("mm"));
            if (min >= 53 || min <=7){
                if (min > 30) {
                    restTime = string.Format("{0:00}00", dt.Hour+1);
                } else {
                    restTime = string.Format("{0:00}00", dt.Hour);
                }
            } else if (min >= 8 && min <= 22) {
                restTime = string.Format("{0:00}15", dt.Hour);
            } else if (min >= 23 && min <= 37) {
                restTime = string.Format("{0:00}30", dt.Hour);
            } else if (min >= 38 && min <= 52) {
                restTime = string.Format("{0:00}45", dt.Hour);
            }

            if (restTime == "0015") {
                DeleteOldData();
            }

            string json = string.Format("\"datetime\" : \"{0}\",\"temp\" : {1},\"hum\" : {2}", dt.ToString(), temp,  hum);
            json = "{" + json + "}";
            Encoding enc = Encoding.UTF8;
            byte[] jsonByteArray = enc.GetBytes(json);
            string url = string.Format("https://airstate.firebaseio.com/measurements/{0}/{1}/{2}.json", pom, restDate, restTime);
            
            WebRequest fireBaseRequest = WebRequest.Create(url);
            fireBaseRequest.Method = "PATCH";
            fireBaseRequest.ContentLength = json.Length;
            fireBaseRequest.ContentType = "application/x-www-form-urlencoded";
            Stream dataStream = fireBaseRequest.GetRequestStream();
            dataStream.Write(jsonByteArray, 0, jsonByteArray.Length);
            dataStream.Close();

            WebResponse response = fireBaseRequest.GetResponse();
            Response.Write(((HttpWebResponse)response).StatusDescription);
            Response.Flush();
            Response.SuppressContent = true;
            ApplicationInstance.CompleteRequest(); 
        } catch(Exception ex) {
            Response.Write(ex.Message);
            Response.Flush();
            Response.SuppressContent = true;
            ApplicationInstance.CompleteRequest(); 
        }
    }

    private void DeleteOldData() {
        string toRestDate = DateTime.Now.AddDays(-2).ToString("yyyyMMdd");
        string url = string.Format("https://airstate.firebaseio.com/measurements/{0}.json?orderBy=\"$key\"&endAt=\"{1}\"", pom, toRestDate);
        WebRequest fireBaseRequest = WebRequest.Create(url);
        fireBaseRequest.Method = "GET";
        WebResponse response = fireBaseRequest.GetResponse();
        String responseString = string.Empty;
        using (Stream stream = response.GetResponseStream()) {
            StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            responseString = reader.ReadToEnd();
        }

        string re2 = "(\")";	// Any Single Character 1
        string re3 = "((?:(?:[1]{1}\\d{1}\\d{1}\\d{1})|(?:[2]{1}\\d{3}))(?:[0]?[1-9]|[1][012])(?:(?:[0-2]?\\d{1})|(?:[3][01]{1})))(?![\\d])";	// YYYYMMDD 1
        string re4 = "(\")";	// Any Single Character 2

        Regex r = new Regex(re2 + re3 + re4, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        MatchCollection matches = r.Matches(responseString);
        if (matches.Count > 0) {
            foreach (Match match in matches) {
                url = string.Format("https://airstate.firebaseio.com/measurements/{0}/{1}.json", pom, match.Value.Replace("\"",string.Empty));
                fireBaseRequest = WebRequest.Create(url);
                fireBaseRequest.Method = "DELETE";
                fireBaseRequest.GetResponse();
            }
        }
    }
}
