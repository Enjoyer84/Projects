﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BettApplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
  public partial class MainWindow : Window
    {

        private string URL = "https://www.mozzartbet.com/MozzartWS/oddsLive/offer";

        private string firstPageParams = @"{""sports"":[1], ""dateRange"":{""from"":null, ""to"":null}, ""matchStatus"":[""READY""], ""matchSorting"":""BY_TIME"", ""selectedCompetitions"":[], ""selectedGames"":null, ""languageId"":1, ""matchNumber"":null, ""favouriteMatchNumbers"":[], ""pageNumber"":1}";

        private int numberOfPages = 1;

        private class Matches
        {
            public string MatchStatus { get; set; }
            public string Time { get; set; }
            public string Home { get; set; }
            public string Away { get; set; }
            public string Status { get; set; }
            public string League { get; set; }
            public decimal k_1 { get; set; }
            public decimal k_X { get; set; }
            public decimal k_2 { get; set; }
        }

        private class Teams
        {
            public string Team { get; set; }
            public string League { get; set; }
        }

        private IList<Matches> myMatches;
        private IList<Matches> allMyMatches;

        private IList<Teams> myHomeTeams;
        private IList<Teams> myAwayTeams;
        private IList<Teams> distinctTeams;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // get data from the first page
            GetPageData(firstPageParams);
            // get data from rest of the pages 
            int i = 2;
            while (i <= numberOfPages)
            {
                var bodyParams = firstPageParams.Substring(0, firstPageParams.Length - 2);
                bodyParams = bodyParams + i.ToString() + "}";// + 
                GetPageData(bodyParams);

                i = i + 1;
            }



            //FillSQLiteDB();   // for testing purposes
        }

        private void GetPageData(string bodyParams)
        {
            WebRequest request = WebRequest.Create(URL);

            request.Credentials = CredentialCache.DefaultCredentials;

            request.Method = "POST";

            byte[] byteArray = Encoding.UTF8.GetBytes(bodyParams);

            request.ContentLength = byteArray.Length;

            request.ContentType = "application/json";

            Stream dataStream = request.GetRequestStream();

            dataStream.Write(byteArray, 0, byteArray.Length);

            dataStream.Close();

            WebResponse response = request.GetResponse();

            Console.WriteLine(((HttpWebResponse)response).StatusDescription);

            dataStream = response.GetResponseStream();

            StreamReader reader = new StreamReader(dataStream);

            string responseFromServer = reader.ReadToEnd();

            HandleJSONResponse(responseFromServer);

            reader.Close();
            dataStream.Close();
            response.Close();

        }

        private void HandleJSONResponse(string responseFromServer)
        {
            JObject o = JObject.Parse(responseFromServer);

            // get page numbers
            JObject pagination = (JObject)o["paginationInfo"];
            numberOfPages = (int)pagination["numberOfTotalPages"];

            JArray matchesArray = (JArray)o["matches"];

            myMatches = matchesArray.Select(p => new Matches
            {
                MatchStatus = (string)p["matchStatus"],
                Time = UnixTimeStampToDateTime((double)p["time"]),
                Home = (string)p["home"],
                Away = (string)p["visitor"],
                Status = (string)p["matchStatus"],
                League = (string)p["competition"]["shortName"],   // to test
                k_1 = (decimal)(p["odds"][0]["subgames"][0]["value"] ?? 1) / 100,
                k_X = (decimal)(p["odds"][0]["subgames"][1]["value"] ?? 1) / 100,
                k_2 = (decimal)(p["odds"][0]["subgames"][2]["value"] ?? 1) / 100
            }).Where(p => p.MatchStatus == "READY")
            .ToList();
            if (allMyMatches == null)
            {
                allMyMatches = myMatches;
            }
            else
            {
                allMyMatches = (allMyMatches.Concat(myMatches)).ToList();
            }



            lvMain.ItemsSource = allMyMatches;// myMatches;

            //------------- ONLY TEAMS ------------// (not used now but will in future)
            //
            //myHomeTeams = matchesArray.Select(p => new Teams
            //{
            //    Team = (string)p["home"],
            //    League = (string)p["competition"]["name"]
            //}).ToList();

            //myAwayTeams = matchesArray.Select(p => new Teams
            //{
            //    Team = (string)p["visitor"],
            //    League = (string)p["competition"]["name"]
            //}).ToList();

            //var AllTeams = myHomeTeams.Concat(myAwayTeams);
            //var AllTeamsList = AllTeams.ToList();

            //distinctTeams = AllTeamsList
            //            .GroupBy(p => new { p.Team, p.League })
            //            .Select(g => g.First())
            //            .ToList();
            //lvMain.ItemsSource = distinctTeams;            
        }

        private void FillSQLiteDB()
        {
            SQLiteConnection sqlite_conn; // declare

            sqlite_conn = new SQLiteConnection(); // Create an instance of the object
            sqlite_conn.ConnectionString = "Data Source=d:\\SQLite DB\\BetAppDB.db;New=False;Compress=True;Version=3;"; // Set the ConnectionString  Compress=True;Version=3;
            sqlite_conn.Open(); // Open the connection. Now you can fire SQL-Queries

            // create a new SQL command:
            var sqlite_cmd = sqlite_conn.CreateCommand();
            //sqlite_cmd.CommandText = "CREATE TABLE MozzartTeams1 (Team varchar(100) primary key, League varchar(100) primary key);";            
            //sqlite_cmd.ExecuteNonQuery();

            //var sqlite_cmd = sqlite_conn.CreateCommand();
            //sqlite_cmd.CommandText = "CREATE TABLE MozzartTeams (Team varchar(100), League varchar(100), UNIQUE(Team, League));";
            //sqlite_cmd.ExecuteNonQuery();

            int i = 0;
            while (i <= distinctTeams.Count() - 1)
            {
                sqlite_cmd.CommandText = "INSERT INTO MozzartTeams(Team, League) values ('" + distinctTeams.ToList()[i].Team + "', '" + distinctTeams.ToList()[i].League + "');";
                sqlite_cmd.ExecuteNonQuery();
                i = i + 1;
            }
        }
        public static string UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds((unixTimeStamp / 1000) + 7200);//.ToLocalTime();
            var Time = dtDateTime.ToString("g", CultureInfo.CreateSpecificCulture("fr-BE"));
            return Time;
        }
    }
}