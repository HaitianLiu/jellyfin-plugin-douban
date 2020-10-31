using System.Collections.Generic;

namespace Jellyfin.Plugin.Douban.Response
{
    internal class Subject
    {
        public string Title { get; set; }
        public string Original_Title { get; set; }
        public string Intro { get; set; }
        public string Year { get; set; }
        public List<string> Pubdate { get; set; }
        public Rating Rating { get; set; }
        public Avatar Images { get; set; }
        public string Url { get; set; }
        public List<string> Countries { get; set; }
        public Trailer Trailer { get; set; }
        public List<PersonInfo> Directors { get; set; }
        public List<PersonInfo> Writers { get; set; }
        public List<PersonInfo> Actors { get; set; }
        public List<string> Genres { get; set; }
        public string Subtype { get; set; }
        // season information
        public int? Seasons_Count { get; set; }
        public int? Current_Season { get; set; }
        public int? Episodes_Count { get; set; }
    }

    internal class Rating
    {
        public float Value { get; set; }
    }

    internal class PersonInfo
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Id { get; set; }
        public Avatar Avatar { get; set; }

        public List<string> Roles { get; set; }

        public string Title { get; set; }

        public string Abstract { get; set; }

        public string Type { get; set; }
    }

    internal class Avatar
    {
        public string Large { get; set; }
    }

    internal class Trailer
    {
        public string Video_Url { get; set; }
    }
}