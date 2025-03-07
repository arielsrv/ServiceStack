﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Funq;
using NUnit.Framework;
using ServiceStack.Data;
using ServiceStack.DataAnnotations;
using ServiceStack.OrmLite;
using ServiceStack.Text;

namespace ServiceStack.WebHost.Endpoints.Tests
{
    public class AutoQueryAppHost : AppSelfHostBase
    {
        public AutoQueryAppHost()
            : base("AutoQuery", typeof(AutoQueryService).Assembly) { }

        public override void Configure(Container container)
        {
            container.Register<IDbConnectionFactory>(
                new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider));

            //container.Register<IDbConnectionFactory>(
            //    new OrmLiteConnectionFactory("Server={0};Database=test;User Id=test;Password=test;".Fmt(Environment.GetEnvironmentVariable("CI_HOST")),
            //        SqlServerDialect.Provider));

            //container.Register<IDbConnectionFactory>(
            //    new OrmLiteConnectionFactory("Server=localhost;Database=test;UID=root;Password=test",
            //        MySqlDialect.Provider));

            //container.Register<IDbConnectionFactory>(
            //    new OrmLiteConnectionFactory("Server=localhost;Port=5432;User Id=test;Password=test;Database=test;Pooling=true;MinPoolSize=0;MaxPoolSize=200",
            //        PostgreSqlDialect.Provider));

            using (var db = container.Resolve<IDbConnectionFactory>().Open())
            {
                db.DropAndCreateTable<Rockstar>();
                db.DropAndCreateTable<RockstarAlbum>();
                db.DropAndCreateTable<RockstarGenre>();
                db.DropAndCreateTable<Movie>();
                db.InsertAll(SeedRockstars);
                db.InsertAll(SeedAlbums);
                db.InsertAll(SeedGenres);
                db.InsertAll(SeedMovies);

                db.DropAndCreateTable<AllFields>();
                db.Insert(new AllFields {
                    Id = 1,
                    NullableId = 2,
                    Byte = 3,
                    DateTime = new DateTime(2001,01,01),
                    NullableDateTime = new DateTime(2002, 02, 02),
                    Decimal = 4,
                    Double = 5.5,
                    Float = 6.6f,
                    Guid = new Guid("3EE6865A-4149-4940-B7A2-F952E0FEFC5E"),
                    NullableGuid = new Guid("7A2FDDD8-4BB0-4735-8230-A6AC79088489"),
                    Long = 7,
                    Short = 8,
                    String = "string",
                    TimeSpan = TimeSpan.FromHours(1),
                    NullableTimeSpan = TimeSpan.FromDays(1),
                    UInt = 9,
                    ULong = 10,
                    UShort = 11,
                });

                db.DropAndCreateTable<Adhoc>();
                db.InsertAll(SeedRockstars.Map(x => new Adhoc {
                    Id = x.Id, FirstName = x.FirstName, LastName = x.LastName
                }));
            }

            var autoQuery = new AutoQueryFeature
                {
                    MaxLimit = 100,
                    EnableRawSqlFilters = true,
                    ResponseFilters = {
                        ctx => {
                            var executedCmds = new List<Command>();
                            var supportedFns = new Dictionary<string, Func<int, int, int>>(StringComparer.OrdinalIgnoreCase)
                            {
                                {"ADD",      (a,b) => a + b },
                                {"MULTIPLY", (a,b) => a * b },
                                {"DIVIDE",   (a,b) => a / b },
                                {"SUBTRACT", (a,b) => a - b },
                            };
                            foreach (var cmd in ctx.Commands)
                            {
                                Func<int, int, int> fn;
                                if (!supportedFns.TryGetValue(cmd.Name, out fn)) continue;
                                var label = !string.IsNullOrWhiteSpace(cmd.Suffix) ? cmd.Suffix.Trim() : cmd.ToString();
                                ctx.Response.Meta[label] = fn(int.Parse(cmd.Args[0]), int.Parse(cmd.Args[1])).ToString();
                                executedCmds.Add(cmd);
                            }
                            ctx.Commands.RemoveAll(executedCmds.Contains);
                        }        
                    }
                }
                .RegisterQueryFilter<QueryRockstarsFilter, Rockstar>((req, q, dto) =>
                    q.And(x => x.LastName.EndsWith("son"))
                )
                .RegisterQueryFilter<QueryCustomRockstarsFilter, Rockstar>((req, q, dto) =>
                    q.And(x => x.LastName.EndsWith("son"))
                )
                .RegisterQueryFilter<IFilterRockstars, Rockstar>((req, q, dto) =>
                    q.And(x => x.LastName.EndsWith("son"))
                );

            Plugins.Add(autoQuery);
        }

        public static Rockstar[] SeedRockstars = new[] {
            new Rockstar { Id = 1, FirstName = "Jimi", LastName = "Hendrix", LivingStatus = LivingStatus.Dead, Age = 27, DateOfBirth = new DateTime(1942, 11, 27), DateDied = new DateTime(1970, 09, 18), },
            new Rockstar { Id = 2, FirstName = "Jim", LastName = "Morrison", Age = 27, LivingStatus = LivingStatus.Dead, DateOfBirth = new DateTime(1943, 12, 08), DateDied = new DateTime(1971, 07, 03),  },
            new Rockstar { Id = 3, FirstName = "Kurt", LastName = "Cobain", Age = 27, LivingStatus = LivingStatus.Dead, DateOfBirth = new DateTime(1967, 02, 20), DateDied = new DateTime(1994, 04, 05), },
            new Rockstar { Id = 4, FirstName = "Elvis", LastName = "Presley", Age = 42, LivingStatus = LivingStatus.Dead, DateOfBirth = new DateTime(1935, 01, 08), DateDied = new DateTime(1977, 08, 16), },
            new Rockstar { Id = 5, FirstName = "David", LastName = "Grohl", Age = 44, LivingStatus = LivingStatus.Alive, DateOfBirth = new DateTime(1969, 01, 14), },
            new Rockstar { Id = 6, FirstName = "Eddie", LastName = "Vedder", Age = 48, LivingStatus = LivingStatus.Alive, DateOfBirth = new DateTime(1964, 12, 23), },
            new Rockstar { Id = 7, FirstName = "Michael", LastName = "Jackson", Age = 50, LivingStatus = LivingStatus.Dead, DateOfBirth = new DateTime(1958, 08, 29), DateDied = new DateTime(2009, 06, 05), },
        };

        public static RockstarAlbum[] SeedAlbums = new[] {
            new RockstarAlbum { RockstarId = 1, Name = "Electric Ladyland" },    
            new RockstarAlbum { RockstarId = 3, Name = "Nevermind" },    
            new RockstarAlbum { RockstarId = 5, Name = "Foo Fighters" },    
            new RockstarAlbum { RockstarId = 6, Name = "Into the Wild" },    
        };

        public static RockstarGenre[] SeedGenres = new[] {
            new RockstarGenre { RockstarId = 1, Name = "Rock" },    
            new RockstarGenre { RockstarId = 3, Name = "Grunge" },    
            new RockstarGenre { RockstarId = 5, Name = "Alternative Rock" },    
            new RockstarGenre { RockstarId = 6, Name = "Folk Rock" },    
        };

        public static Movie[] SeedMovies = new[] {
			new Movie { ImdbId = "tt0111161", Title = "The Shawshank Redemption", Score = 9.2m, Director = "Frank Darabont", ReleaseDate = new DateTime(1995,2,17), TagLine = "Fear can hold you prisoner. Hope can set you free.", Genres = new List<string>{"Crime","Drama"}, Rating = "R", },
			new Movie { ImdbId = "tt0068646", Title = "The Godfather", Score = 9.2m, Director = "Francis Ford Coppola", ReleaseDate = new DateTime(1972,3,24), TagLine = "An offer you can't refuse.", Genres = new List<string> {"Crime","Drama", "Thriller"}, Rating = "R", },
			new Movie { ImdbId = "tt1375666", Title = "Inception", Score = 9.2m, Director = "Christopher Nolan", ReleaseDate = new DateTime(2010,7,16), TagLine = "Your mind is the scene of the crime", Genres = new List<string>{"Action", "Mystery", "Sci-Fi", "Thriller"}, Rating = "PG-13", },
			new Movie { ImdbId = "tt0071562", Title = "The Godfather: Part II", Score = 9.0m, Director = "Francis Ford Coppola", ReleaseDate = new DateTime(1974,12,20), Genres = new List<string> {"Crime","Drama", "Thriller"}, Rating = "R", },
			new Movie { ImdbId = "tt0060196", Title = "The Good, the Bad and the Ugly", Score = 9.0m, Director = "Sergio Leone", ReleaseDate = new DateTime(1967,12,29), TagLine = "They formed an alliance of hate to steal a fortune in dead man's gold", Genres = new List<string>{"Adventure","Western"}, Rating = "R", },
			new Movie { ImdbId = "tt0114709", Title = "Toy Story", Score = 8.3m, Director = "John Lasseter", ReleaseDate = new DateTime(1995,11,22), TagLine = "A cowboy doll is profoundly threatened and jealous when a new spaceman figure supplants him as top toy in a boy's room.", Genres = new List<string>{"Animation","Adventure","Comedy"}, Rating = "G", },
			new Movie { ImdbId = "tt2294629", Title = "Frozen", Score = 7.8m, Director = "Chris Buck", ReleaseDate = new DateTime(2013,11,27), TagLine = "Fearless optimist Anna teams up with Kristoff in an epic journey, encountering Everest-like conditions, and a hilarious snowman named Olaf", Genres = new List<string>{"Animation","Adventure","Comedy"}, Rating = "PG", },
			new Movie { ImdbId = "tt1453405", Title = "Monsters University", Score = 7.4m, Director = "Dan Scanlon", ReleaseDate = new DateTime(2013,06,21), TagLine = "A look at the relationship between Mike and Sulley during their days at Monsters University -- when they weren't necessarily the best of friends.", Genres = new List<string>{"Animation","Adventure","Comedy"}, Rating = "G", },
			new Movie { ImdbId = "tt0468569", Title = "The Dark Knight", Score = 9.0m, Director = "Christopher Nolan", ReleaseDate = new DateTime(2008,07,18), TagLine = "When Batman, Gordon and Harvey Dent launch an assault on the mob, they let the clown out of the box, the Joker, bent on turning Gotham on itself and bringing any heroes down to his level.", Genres = new List<string>{"Action","Crime","Drama"}, Rating = "PG-13", },
			new Movie { ImdbId = "tt0109830", Title = "Forrest Gump", Score = 8.8m, Director = "Robert Zemeckis", ReleaseDate = new DateTime(1996,07,06), TagLine = "Forrest Gump, while not intelligent, has accidentally been present at many historic moments, but his true love, Jenny Curran, eludes him.", Genres = new List<string>{"Drama","Romance"}, Rating = "PG-13", },
        };
    }
    
    [Route("/query/rockstars")]
    public class QueryRockstars : QueryBase<Rockstar>
    {
        public int? Age { get; set; }
        //public LivingStatus? LivingStatus { get; set; }
    }

    public class QueryRockstarsConventions : QueryBase<Rockstar>
    {
        public DateTime? DateOfBirthGreaterThan { get; set; }
        public DateTime? DateDiedLessThan { get; set; }
        public int[] Ids { get; set; }
        public int? AgeOlderThan { get; set; }
        public int? AgeGreaterThanOrEqualTo { get; set; }
        public int? AgeGreaterThan { get; set; }
        public int? GreaterThanAge { get; set; }
        public string FirstNameStartsWith { get; set; }
        public string LastNameEndsWith { get; set; }
        public string LastNameContains { get; set; }
        public string RockstarAlbumNameContains { get; set; }
        public int? RockstarIdAfter { get; set; }
        public int? RockstarIdOnOrAfter { get; set; }
    }

    public class QueryCustomRockstars : QueryBase<Rockstar, CustomRockstar>
    {
        public int? Age { get; set; }
    }

    [Route("/customrockstars")]
    public class QueryRockstarAlbums : QueryBase<Rockstar, CustomRockstar>, IJoin<Rockstar, RockstarAlbum>
    {
        public int? Age { get; set; }
        public string RockstarAlbumName { get; set; }
    }

    public class QueryRockstarAlbumsImplicit : QueryBase<Rockstar, CustomRockstar>, IJoin<Rockstar, RockstarAlbum>
    {
    }

    public class QueryRockstarAlbumsLeftJoin : QueryBase<Rockstar, CustomRockstar>, ILeftJoin<Rockstar, RockstarAlbum>
    {
        public int? Age { get; set; }
        public string AlbumName { get; set; }
    }

    public class QueryMultiJoinRockstar : QueryBase<Rockstar, CustomRockstar>, 
        IJoin<Rockstar, RockstarAlbum>,
        IJoin<Rockstar, RockstarGenre>
    {
        public int? Age { get; set; }
        public string RockstarAlbumName { get; set; }
        public string RockstarGenreName { get; set; }
    }



    public class QueryOverridedRockstars : QueryBase<Rockstar>
    {
        public int? Age { get; set; }
    }

    public class QueryOverridedCustomRockstars : QueryBase<Rockstar, CustomRockstar>
    {
        public int? Age { get; set; }
    }

    public class QueryFieldRockstars : QueryBase<Rockstar>
    {
        public string FirstName { get; set; } //default to 'AND FirstName = {Value}'

        public string[] FirstNames { get; set; } //Collections default to 'FirstName IN ({Values})

        [QueryField(Operand = ">=")]
        public int? Age { get; set; }

        [QueryField(Template = "UPPER({Field}) LIKE UPPER({Value})", Field = "FirstName")]
        public string FirstNameCaseInsensitive { get; set; }

        [QueryField(Template = "{Field} LIKE {Value}", Field = "FirstName", ValueFormat = "{0}%")]
        public string FirstNameStartsWith { get; set; }

        [QueryField(Template = "{Field} LIKE {Value}", Field = "LastName", ValueFormat = "%{0}")]
        public string LastNameEndsWith { get; set; }

        [QueryField(Template = "{Field} BETWEEN {Value1} AND {Value2}", Field = "FirstName")]
        public string[] FirstNameBetween { get; set; }

        [QueryField(Term = QueryTerm.Or, Template = "UPPER({Field}) LIKE UPPER({Value})", Field = "LastName")]
        public string OrLastName { get; set; }
    }

    public class QueryFieldRockstarsDynamic : QueryBase<Rockstar>
    {
        public int? Age { get; set; }
    }

    public class QueryRockstarsFilter : QueryBase<Rockstar>
    {
        public int? Age { get; set; }
    }

    public class QueryCustomRockstarsFilter : QueryBase<Rockstar, CustomRockstar>
    {
        public int? Age { get; set; }
    }

    public interface IFilterRockstars { }
    public class QueryRockstarsIFilter : QueryBase<Rockstar>, IFilterRockstars
    {
        public int? Age { get; set; }
    }

    [Query(QueryTerm.Or)]
    [Route("/OrRockstars")]
    public class QueryOrRockstars : QueryBase<Rockstar>
    {
        public int? Age { get; set; }
        public string FirstName { get; set; }
    }

    [Query(QueryTerm.Or)]
    public class QueryGetRockstars : QueryBase<Rockstar>
    {
        public int[] Ids { get; set; }
        public List<int> Ages { get; set; }
        public List<string> FirstNames { get; set; }
        public int[] IdsBetween { get; set; }
    }

    [Query(QueryTerm.Or)]
    public class QueryGetRockstarsDynamic : QueryBase<Rockstar> {}

    public class RockstarAlbum
    {
        [AutoIncrement]
        public int Id { get; set; }
        public int RockstarId { get; set; }
        public string Name { get; set; }
    }

    public class RockstarGenre
    {
        [AutoIncrement]
        public int Id { get; set; }
        public int RockstarId { get; set; }
        public string Name { get; set; }
    }

    public class CustomRockstar
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int? Age { get; set; }
        public string RockstarAlbumName { get; set; }
        public string RockstarGenreName { get; set; }
    }

    [Route("/movies/search")]
    [Query(QueryTerm.And)] //Default
    public class SearchMovies : QueryBase<Movie> {}

    [Route("/movies")]
    [Query(QueryTerm.Or)]
    public class QueryMovies : QueryBase<Movie>
    {
        public int[] Ids { get; set; }
        public string[] ImdbIds { get; set; }
        public string[] Ratings { get; set; }
    }

    public class Movie
    {
        [AutoIncrement]
        public int Id { get; set; }
        public string ImdbId { get; set; }
        public string Title { get; set; }
        public string Rating { get; set; }
        public decimal Score { get; set; }
        public string Director { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string TagLine { get; set; }
        public List<string> Genres { get; set; }
    }

    public class StreamMovies : QueryBase<Movie>
    {
        public string[] Ratings { get; set; }
    }

    public class QueryUnknownRockstars : QueryBase<Rockstar>
    {
        public int UnknownInt { get; set; }
        public string UnknownProperty { get; set; }

    }
    [Route("/query/rockstar-references")]
    public class QueryRockstarsWithReferences : QueryBase<RockstarReference>
    {
        public int? Age { get; set; }
    }

    [Alias("Rockstar")]
    public class RockstarReference
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int? Age { get; set; }

        [Reference]
        public List<RockstarAlbum> Albums { get; set; } 
    }

    public class QueryAllFields : QueryBase<AllFields>
    {
        public virtual Guid Guid { get; set; }
    }

    public class AllFields
    {
        public virtual int Id { get; set; }
        public virtual int? NullableId { get; set; }
        public virtual byte Byte { get; set; }
        public virtual short Short { get; set; }
        public virtual int Int { get; set; }
        public virtual long Long { get; set; }
        public virtual ushort UShort { get; set; }
        public virtual uint UInt { get; set; }
        public virtual ulong ULong { get; set; }
        public virtual float Float { get; set; }
        public virtual double Double { get; set; }
        public virtual decimal Decimal { get; set; }
        public virtual string String { get; set; }
        public virtual DateTime DateTime { get; set; }
        public virtual TimeSpan TimeSpan { get; set; }
        public virtual Guid Guid { get; set; }
        public virtual DateTime? NullableDateTime { get; set; }
        public virtual TimeSpan? NullableTimeSpan { get; set; }
        public virtual Guid? NullableGuid { get; set; }
    }

    [DataContract]
    public class Adhoc
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember(Name = "first_name")]
        public string FirstName { get; set; }

        [DataMember]
        public string LastName { get; set; }
    }

    [DataContract]
    [Route("/adhoc-rockstars")]
    public class QueryAdhocRockstars : QueryBase<Rockstar>
    {
        [DataMember(Name = "first_name")]
        public string FirstName { get; set; }
    }

    [DataContract]
    [Route("/adhoc")]
    public class QueryAdhoc : QueryBase<Adhoc> {}

    public class AutoQueryService : Service
    {
        public IAutoQuery AutoQuery { get; set; }

        //Override with custom impl
        public object Any(QueryOverridedRockstars dto)
        {
            var q = AutoQuery.CreateQuery(dto, Request.GetRequestParams());
            q.Take(1);
            return AutoQuery.Execute(dto, q);
        }

        public object Any(QueryOverridedCustomRockstars dto)
        {
            var q = AutoQuery.CreateQuery(dto, Request.GetRequestParams());
            q.Take(1);
            return AutoQuery.Execute(dto, q);
        }

        public object Any(StreamMovies dto)
        {
            var q = AutoQuery.CreateQuery(dto, Request.GetRequestParams());
            q.Take(2);
            return AutoQuery.Execute(dto, q);
        }
    }

    [TestFixture]
    public class AutoQueryTests
    {
        private readonly ServiceStackHost appHost;
        public IServiceClient client;

        private static readonly int TotalRockstars = AutoQueryAppHost.SeedRockstars.Length;
        private static readonly int TotalAlbums = AutoQueryAppHost.SeedAlbums.Length;

        public AutoQueryTests()
        {
            appHost = new AutoQueryAppHost()
                .Init()
                .Start(Config.ListeningOn);

            client = new JsonServiceClient(Config.ListeningOn);
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            appHost.Dispose();
        }

        [NUnit.Framework.Ignore("Debug Run")]
        [Test]
        public void RunFor10Mins()
        {
            Process.Start(Config.ListeningOn);
            Thread.Sleep(TimeSpan.FromMinutes(10));
        }

        [Test]
        public void Can_execute_basic_query()
        {
            var response = client.Get(new QueryRockstars());

            Assert.That(response.Offset, Is.EqualTo(0));
            Assert.That(response.Total, Is.EqualTo(TotalRockstars));
            Assert.That(response.Results.Count, Is.EqualTo(TotalRockstars));
        }

        [Test]
        public void Can_execute_overridden_basic_query()
        {
            var response = client.Get(new QueryOverridedRockstars());

            Assert.That(response.Offset, Is.EqualTo(0));
            Assert.That(response.Total, Is.EqualTo(TotalRockstars));
            Assert.That(response.Results.Count, Is.EqualTo(1));
        }

        [Test]
        public void Can_execute_AdhocRockstars_query()
        {
            var request = new QueryAdhocRockstars { FirstName = "Jimi" };

            Assert.That(request.ToGetUrl(), Is.EqualTo("/adhoc-rockstars?first_name=Jimi"));

            var response = client.Get(request);

            Assert.That(response.Offset, Is.EqualTo(0));
            Assert.That(response.Total, Is.EqualTo(1));
            Assert.That(response.Results.Count, Is.EqualTo(1));
            Assert.That(response.Results[0].FirstName, Is.EqualTo(request.FirstName));
        }

        [Test]
        public void Can_execute_Adhoc_query_alias()
        {
            var response = Config.ListeningOn.CombineWith("adhoc")
                .AddQueryParam("first_name", "Jimi")
                .GetJsonFromUrl()
                .FromJson<QueryResponse<Adhoc>>();

            Assert.That(response.Results.Count, Is.EqualTo(1));
            Assert.That(response.Results[0].FirstName, Is.EqualTo("Jimi"));
        }

        [Test]
        public void Can_execute_Adhoc_query_convention()
        {
            var response = Config.ListeningOn.CombineWith("adhoc")
                .AddQueryParam("last_name", "Hendrix")
                .GetJsonFromUrl()
                .FromJson<QueryResponse<Adhoc>>();
            Assert.That(response.Results.Count, Is.EqualTo(7));

            JsConfig.EmitLowercaseUnderscoreNames = true;
            response = Config.ListeningOn.CombineWith("adhoc")
                .AddQueryParam("last_name", "Hendrix")
                .GetJsonFromUrl()
                .FromJson<QueryResponse<Adhoc>>();
            JsConfig.Reset();

            Assert.That(response.Results.Count, Is.EqualTo(1));
            Assert.That(response.Results[0].FirstName, Is.EqualTo("Jimi"));
        }

        [Test]
        public void Can_execute_explicit_equality_condition_on_overridden_CustomRockstar()
        {
            var response = client.Get(new QueryOverridedCustomRockstars { Age = 27 });

            Assert.That(response.Total, Is.EqualTo(3));
            Assert.That(response.Results.Count, Is.EqualTo(1));
        }

        [Test]
        public void Can_execute_basic_query_with_limits()
        {
            var response = client.Get(new QueryRockstars { Skip = 2 });
            Assert.That(response.Offset, Is.EqualTo(2));
            Assert.That(response.Total, Is.EqualTo(TotalRockstars));
            Assert.That(response.Results.Count, Is.EqualTo(TotalRockstars - 2));

            response = client.Get(new QueryRockstars { Take = 2 });
            Assert.That(response.Offset, Is.EqualTo(0));
            Assert.That(response.Total, Is.EqualTo(TotalRockstars));
            Assert.That(response.Results.Count, Is.EqualTo(2));

            response = client.Get(new QueryRockstars { Skip = 2, Take = 2 });
            Assert.That(response.Offset, Is.EqualTo(2));
            Assert.That(response.Total, Is.EqualTo(TotalRockstars));
            Assert.That(response.Results.Count, Is.EqualTo(2));
        }

        [Test]
        public void Can_execute_explicit_equality_condition()
        {
            var response = client.Get(new QueryRockstars { Age = 27 });

            Assert.That(response.Total, Is.EqualTo(3));
            Assert.That(response.Results.Count, Is.EqualTo(3));
        }

        [Test]
        public void Can_execute_explicit_equality_condition_on_CustomRockstar()
        {
            var response = client.Get(new QueryCustomRockstars { Age = 27 });

            Assert.That(response.Total, Is.EqualTo(3));
            Assert.That(response.Results.Count, Is.EqualTo(3));
        }

        [Test]
        public void Can_execute_implicit_equality_condition()
        {
            var response = Config.ListeningOn.CombineWith("json/reply/QueryRockstars")
                .AddQueryParam("FirstName", "Jim")
                .AddQueryParam("LivingStatus", "Dead")
                .GetJsonFromUrl()
                .FromJson<QueryResponse<Rockstar>>();

            Assert.That(response.Total, Is.EqualTo(1));
            Assert.That(response.Results.Count, Is.EqualTo(1));
            Assert.That(response.Results[0].LastName, Is.EqualTo("Morrison"));
        }

        [Test]
        public void Can_execute_query_with_JOIN_on_RockstarAlbums()
        {
            var response = client.Get(new QueryRockstarAlbums());
            Assert.That(response.Total, Is.EqualTo(TotalAlbums));
            Assert.That(response.Results.Count, Is.EqualTo(TotalAlbums));
            var albumNames = response.Results.Select(x => x.RockstarAlbumName);
            Assert.That(albumNames, Is.EquivalentTo(new[] {
                "Electric Ladyland", "Nevermind", "Foo Fighters", "Into the Wild"
            }));

            response = client.Get(new QueryRockstarAlbums { Age = 27 });
            Assert.That(response.Total, Is.EqualTo(2));
            Assert.That(response.Results.Count, Is.EqualTo(2));
            albumNames = response.Results.Select(x => x.RockstarAlbumName);
            Assert.That(albumNames, Is.EquivalentTo(new[] {
                "Electric Ladyland", "Nevermind"
            }));

            response = client.Get(new QueryRockstarAlbums { RockstarAlbumName = "Nevermind" });
            Assert.That(response.Total, Is.EqualTo(1));
            Assert.That(response.Results.Count, Is.EqualTo(1));
            albumNames = response.Results.Select(x => x.RockstarAlbumName);
            Assert.That(albumNames, Is.EquivalentTo(new[] { "Nevermind" }));
        }

        [Test]
        public void Can_execute_query_with_multiple_JOINs_on_Rockstar_Albums_and_Genres()
        {
            var response = client.Get(new QueryMultiJoinRockstar());
            Assert.That(response.Total, Is.EqualTo(TotalAlbums));
            Assert.That(response.Results.Count, Is.EqualTo(TotalAlbums));
            var albumNames = response.Results.Select(x => x.RockstarAlbumName);
            Assert.That(albumNames, Is.EquivalentTo(new[] {
                "Electric Ladyland", "Nevermind", "Foo Fighters", "Into the Wild"
            }));

            var genreNames = response.Results.Select(x => x.RockstarGenreName);
            Assert.That(genreNames, Is.EquivalentTo(new[] {
                "Rock", "Grunge", "Alternative Rock", "Folk Rock"
            }));

            response = client.Get(new QueryMultiJoinRockstar { RockstarAlbumName = "Nevermind" });
            Assert.That(response.Total, Is.EqualTo(1));
            Assert.That(response.Results.Count, Is.EqualTo(1));
            albumNames = response.Results.Select(x => x.RockstarAlbumName);
            Assert.That(albumNames, Is.EquivalentTo(new[] { "Nevermind" }));

            response = client.Get(new QueryMultiJoinRockstar { RockstarGenreName = "Folk Rock" });
            Assert.That(response.Total, Is.EqualTo(1));
            Assert.That(response.Results.Count, Is.EqualTo(1));
            albumNames = response.Results.Select(x => x.RockstarGenreName);
            Assert.That(albumNames, Is.EquivalentTo(new[] { "Folk Rock" }));
        }

        [Test]
        public void Can_execute_IMPLICIT_query_with_JOIN_on_RockstarAlbums()
        {
            var response = Config.ListeningOn.CombineWith("json/reply/QueryRockstarAlbumsImplicit")
                .AddQueryParam("Age", "27")
                .GetJsonFromUrl()
                .FromJson<QueryResponse<CustomRockstar>>();
            Assert.That(response.Total, Is.EqualTo(2));
            Assert.That(response.Results.Count, Is.EqualTo(2));
            var albumNames = response.Results.Select(x => x.RockstarAlbumName);
            Assert.That(albumNames, Is.EquivalentTo(new[] {
                "Electric Ladyland", "Nevermind"
            }));

            response = Config.ListeningOn.CombineWith("json/reply/QueryRockstarAlbumsImplicit")
                .AddQueryParam("RockstarAlbumName", "Nevermind")
                .GetJsonFromUrl()
                .FromJson<QueryResponse<CustomRockstar>>();
            Assert.That(response.Total, Is.EqualTo(1));
            Assert.That(response.Results.Count, Is.EqualTo(1));
            albumNames = response.Results.Select(x => x.RockstarAlbumName);
            Assert.That(albumNames, Is.EquivalentTo(new[] { "Nevermind" }));
        }

        [Test]
        public void Can_execute_query_with_LEFTJOIN_on_RockstarAlbums()
        {
            var response = client.Get(new QueryRockstarAlbumsLeftJoin());
            Assert.That(response.Total, Is.EqualTo(TotalRockstars));
            Assert.That(response.Results.Count, Is.EqualTo(TotalRockstars));
            var albumNames = response.Results.Where(x => x.RockstarAlbumName != null).Select(x => x.RockstarAlbumName);
            Assert.That(albumNames, Is.EquivalentTo(new[] {
                "Electric Ladyland", "Nevermind", "Foo Fighters", "Into the Wild"
            }));
        }

        [Test]
        public void Can_execute_custom_QueryFields()
        {
            QueryResponse<Rockstar> response;
            response = client.Get(new QueryFieldRockstars { FirstName = "Jim" });
            Assert.That(response.Results.Count, Is.EqualTo(1));

            response = client.Get(new QueryFieldRockstars { FirstNames = new[] { "Jim","Kurt" } });
            Assert.That(response.Results.Count, Is.EqualTo(2));

            response = client.Get(new QueryFieldRockstars { FirstNameCaseInsensitive = "jim" });
            Assert.That(response.Results.Count, Is.EqualTo(1));

            response = client.Get(new QueryFieldRockstars { FirstNameStartsWith = "Jim" });
            Assert.That(response.Results.Count, Is.EqualTo(2));

            response = client.Get(new QueryFieldRockstars { LastNameEndsWith = "son" });
            Assert.That(response.Results.Count, Is.EqualTo(2));

            response = client.Get(new QueryFieldRockstars { FirstNameBetween = new[] {"A","F"} });
            Assert.That(response.Results.Count, Is.EqualTo(3));

            response = client.Get(new QueryFieldRockstars
            {
                LastNameEndsWith = "son",
                OrLastName = "Hendrix"
            });
            Assert.That(response.Results.Count, Is.EqualTo(3));

            response = client.Get(new QueryFieldRockstars { Age = 42 });
            Assert.That(response.Results.Count, Is.EqualTo(4));
        }

        [Test]
        public void Can_execute_combination_of_QueryFields()
        {
            QueryResponse<Rockstar> response;

            response = client.Get(new QueryFieldRockstars
            {
                FirstNameStartsWith = "Jim",
                LastNameEndsWith = "son",
            });
            Assert.That(response.Results.Count, Is.EqualTo(1));

            response = client.Get(new QueryFieldRockstars
            {
                FirstNameStartsWith = "Jim",
                OrLastName = "Cobain",
            });
            Assert.That(response.Results.Count, Is.EqualTo(3));
        }

        [Test]
        public void Does_escape_values()
        {
            QueryResponse<Rockstar> response;

            response = client.Get(new QueryFieldRockstars
            {
                FirstNameStartsWith = "Jim'\"",
            });
            Assert.That(response.Results.Count, Is.EqualTo(0));
        }

        [Test]
        public void Does_allow_adding_attributes_dynamically()
        {
            typeof(QueryFieldRockstarsDynamic)
                .GetProperty("Age")
                .AddAttributes(new QueryFieldAttribute { Operand = ">=" });

            var response = client.Get(new QueryFieldRockstars { Age = 42 });
            Assert.That(response.Results.Count, Is.EqualTo(4));
        }

        [Test]
        public void Does_execute_typed_QueryFilters()
        {
            // QueryFilter appends additional: x => x.LastName.EndsWith("son")
            var response = client.Get(new QueryRockstarsFilter { Age = 27 });
            Assert.That(response.Results.Count, Is.EqualTo(1));

            var custom = client.Get(new QueryCustomRockstarsFilter { Age = 27 });
            Assert.That(custom.Results.Count, Is.EqualTo(1));

            response = client.Get(new QueryRockstarsIFilter { Age = 27 });
            Assert.That(response.Results.Count, Is.EqualTo(1));
        }

        [Test]
        public void Can_execute_OR_QueryFilters()
        {
            var response = client.Get(new QueryOrRockstars { Age = 42, FirstName = "Jim" });
            Assert.That(response.Results.Count, Is.EqualTo(2));

            response = Config.ListeningOn.CombineWith("OrRockstars")
                .AddQueryParam("Age", "27")
                .AddQueryParam("FirstName", "Kurt")
                .AddQueryParam("LastName", "Hendrix")
                .GetJsonFromUrl()
                .FromJson<QueryResponse<Rockstar>>();
            Assert.That(response.Results.Count, Is.EqualTo(3));
        }

        [Test]
        public void Can_execute_implicit_conventions()
        {
            var baseUrl = Config.ListeningOn.CombineWith("json/reply/QueryRockstars");

            var response = baseUrl.AddQueryParam("AgeOlderThan", 42).AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(3));

            response = baseUrl.AddQueryParam("AgeGreaterThanOrEqualTo", 42).AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(4));

            response = baseUrl.AddQueryParam("AgeGreaterThan", 42).AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(3));
            response = baseUrl.AddQueryParam("GreaterThanAge", 42).AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(3));

            response = baseUrl.AddQueryParam(">Age", 42).AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(4));
            response = baseUrl.AddQueryParam("Age>", 42).AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(3));
            response = baseUrl.AddQueryParam("<Age", 42).AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(3));
            response = baseUrl.AddQueryParam("Age<", 42).AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(4));

            response = baseUrl.AddQueryParam("FirstNameStartsWith", "jim").AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(2));
            response = baseUrl.AddQueryParam("LastNameEndsWith", "son").AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(2));
            response = baseUrl.AddQueryParam("LastNameContains", "e").AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(3));
        }

        [Test]
        public void Can_execute_implicit_conventions_on_JOIN()
        {
            var baseUrl = Config.ListeningOn.CombineWith("json/reply/QueryRockstarAlbums");

            var response = baseUrl.AddQueryParam("RockstarAlbumNameContains", "n").AsJsonInto<CustomRockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(3));

            response = baseUrl.AddQueryParam(">RockstarId", "3").AsJsonInto<CustomRockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(3));
            response = baseUrl.AddQueryParam("RockstarId>", "3").AsJsonInto<CustomRockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(2));
        }

        [Test]
        public void Can_execute_Explicit_conventions()
        {
            var response = client.Get(new QueryRockstarsConventions { Ids = new[] {1, 2, 3} });
            Assert.That(response.Results.Count, Is.EqualTo(3));

            response = client.Get(new QueryRockstarsConventions { AgeOlderThan = 42 });
            Assert.That(response.Results.Count, Is.EqualTo(3));

            response = client.Get(new QueryRockstarsConventions { AgeGreaterThanOrEqualTo = 42 });
            Assert.That(response.Results.Count, Is.EqualTo(4));

            response = client.Get(new QueryRockstarsConventions { AgeGreaterThan = 42 });
            Assert.That(response.Results.Count, Is.EqualTo(3));
            response = client.Get(new QueryRockstarsConventions { GreaterThanAge = 42 });
            Assert.That(response.Results.Count, Is.EqualTo(3));

            response = client.Get(new QueryRockstarsConventions { FirstNameStartsWith = "Jim" });
            Assert.That(response.Results.Count, Is.EqualTo(2));
            response = client.Get(new QueryRockstarsConventions { LastNameEndsWith = "son" });
            Assert.That(response.Results.Count, Is.EqualTo(2));
            response = client.Get(new QueryRockstarsConventions { LastNameContains = "e" });
            Assert.That(response.Results.Count, Is.EqualTo(3));

            response = client.Get(new QueryRockstarsConventions { DateOfBirthGreaterThan = new DateTime(1960, 01, 01) });
            Assert.That(response.Results.Count, Is.EqualTo(3));
            response = client.Get(new QueryRockstarsConventions { DateDiedLessThan = new DateTime(1980, 01, 01) });
            Assert.That(response.Results.Count, Is.EqualTo(3));
        }

        [Test]
        public void Can_execute_where_SqlFilter()
        {
            var baseUrl = Config.ListeningOn.CombineWith("json/reply/QueryRockstars");

            var response = baseUrl.AddQueryParam("_where", "Age > 42").AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(3));
            response = baseUrl.AddQueryParam("_where", "Age >= 42").AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(4));

            response = baseUrl.AddQueryParam("_where", "FirstName".SqlColumn() + " LIKE 'Jim%'").AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(2));
            response = baseUrl.AddQueryParam("_where", "LastName".SqlColumn() + " LIKE '%son'").AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(2));
            response = baseUrl.AddQueryParam("_where", "LastName".SqlColumn() + " LIKE '%e%'").AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(3));

            response = baseUrl
                .AddQueryParam("_select", "r.*")
                .AddQueryParam("_from", "{0} r INNER JOIN {1} a ON r.{2} = a.{3}".Fmt(
                    "Rockstar".SqlTable(), "RockstarAlbum".SqlTable(), 
                    "Id".SqlColumn(),      "RockstarId".SqlColumn()))
                .AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(TotalAlbums));

            response = baseUrl
                .AddQueryParam("_select", "FirstName".SqlColumn())
                .AddQueryParam("_where", "LastName".SqlColumn() + " = 'Cobain'")
                .AsJsonInto<Rockstar>();
            var row = response.Results[0];
            Assert.That(row.Id, Is.EqualTo(default(int)));
            Assert.That(row.FirstName, Is.EqualTo("Kurt"));
            Assert.That(row.LastName, Is.Null);
            Assert.That(row.Age, Is.Null);
        }

        [Test]
        public void Can_execute_In_OR_Queries()
        {
            QueryResponse<Rockstar> response;
            response = client.Get(new QueryGetRockstars());
            Assert.That(response.Results.Count, Is.EqualTo(0));

            response = client.Get(new QueryGetRockstars { Ids = new[] { 1, 2, 3 } });
            Assert.That(response.Results.Count, Is.EqualTo(3));

            response = client.Get(new QueryGetRockstars { Ages = new[] { 42, 44 }.ToList() });
            Assert.That(response.Results.Count, Is.EqualTo(2));

            response = client.Get(new QueryGetRockstars { FirstNames = new[] { "Jim", "Kurt" }.ToList() });
            Assert.That(response.Results.Count, Is.EqualTo(2));

            response = client.Get(new QueryGetRockstars { IdsBetween = new[] { 1, 3 } });
            Assert.That(response.Results.Count, Is.EqualTo(3));
        }

        [Test]
        public void Can_execute_In_OR_Queries_with_implicit_conventions()
        {
            var baseUrl = Config.ListeningOn.CombineWith("json/reply/QueryGetRockstarsDynamic");

            QueryResponse<Rockstar> response;
            response = baseUrl.AddQueryParam("Ids", "1,2,3").AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(3));

            response = baseUrl.AddQueryParam("Ages", "42, 44").AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(2));

            response = baseUrl.AddQueryParam("FirstNames", "Jim,Kurt").AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(2));

            response = baseUrl.AddQueryParam("IdsBetween", "1,3").AsJsonInto<Rockstar>();
            Assert.That(response.Results.Count, Is.EqualTo(3));
        }

        [Test]
        public void Can_query_Movie_Ratings()
        {
            var response = client.Get(new QueryMovies { Ratings = new[] {"G","PG-13"} });
            Assert.That(response.Results.Count, Is.EqualTo(5));

            var url = Config.ListeningOn + "movies?ratings=G,PG-13";
            response = url.AsJsonInto<Movie>();
            Assert.That(response.Results.Count, Is.EqualTo(5));

            response = client.Get(new QueryMovies {
                Ids = new[] { 1, 2 },
                ImdbIds = new[] { "tt0071562", "tt0060196" },
                Ratings = new[] { "G", "PG-13" }
            });
            Assert.That(response.Results.Count, Is.EqualTo(9));

            url = Config.ListeningOn + "movies?ratings=G,PG-13&ids=1,2&imdbIds=tt0071562,tt0060196";
            response = url.AsJsonInto<Movie>();
            Assert.That(response.Results.Count, Is.EqualTo(9));
        }

        [Test]
        public void Can_StreamMovies()
        {
            var results = client.GetLazy(new StreamMovies()).ToList();
            Assert.That(results.Count, Is.EqualTo(10));

            results = client.GetLazy(new StreamMovies { Ratings = new[]{"G","PG-13"} }).ToList();
            Assert.That(results.Count, Is.EqualTo(5));
        }

        [Test]
        public void Does_implicitly_OrderBy_PrimaryKey_when_limits_is_specified()
        {
            var movies = client.Get(new SearchMovies { Take = 100 });
            var ids = movies.Results.Map(x => x.Id);
            var orderedIds = ids.OrderBy(x => x);
            Assert.That(ids, Is.EqualTo(orderedIds));

            var rockstars = client.Get(new SearchMovies { Take = 100 });
            ids = rockstars.Results.Map(x => x.Id);
            orderedIds = ids.OrderBy(x => x);
            Assert.That(ids, Is.EqualTo(orderedIds));
        }

        [Test]
        public void Can_OrderBy_queries()
        {
            var movies = client.Get(new SearchMovies { Take = 100, OrderBy = "ImdbId" });
            var ids = movies.Results.Map(x => x.ImdbId);
            var orderedIds = ids.OrderBy(x => x).ToList();
            Assert.That(ids, Is.EqualTo(orderedIds));

            movies = client.Get(new SearchMovies { Take = 100, OrderBy = "Rating,ImdbId" });
            ids = movies.Results.Map(x => x.ImdbId);
            orderedIds = movies.Results.OrderBy(x => x.Rating).ThenBy(x => x.ImdbId).Map(x => x.ImdbId);
            Assert.That(ids, Is.EqualTo(orderedIds));

            movies = client.Get(new SearchMovies { Take = 100, OrderByDesc = "ImdbId" });
            ids = movies.Results.Map(x => x.ImdbId);
            orderedIds = ids.OrderByDescending(x => x).ToList();
            Assert.That(ids, Is.EqualTo(orderedIds));

            movies = client.Get(new SearchMovies { Take = 100, OrderByDesc = "Rating,ImdbId" });
            ids = movies.Results.Map(x => x.ImdbId);
            orderedIds = movies.Results.OrderByDescending(x => x.Rating)
                .ThenByDescending(x => x.ImdbId).Map(x => x.ImdbId);
            Assert.That(ids, Is.EqualTo(orderedIds));

            movies = client.Get(new SearchMovies { Take = 100, OrderBy = "Rating,-ImdbId" });
            ids = movies.Results.Map(x => x.ImdbId);
            orderedIds = movies.Results.OrderBy(x => x.Rating)
                .ThenByDescending(x => x.ImdbId).Map(x => x.ImdbId);
            Assert.That(ids, Is.EqualTo(orderedIds));

            movies = client.Get(new SearchMovies { Take = 100, OrderByDesc = "Rating,-ImdbId" });
            ids = movies.Results.Map(x => x.ImdbId);
            orderedIds = movies.Results.OrderByDescending(x => x.Rating)
                .ThenBy(x => x.ImdbId).Map(x => x.ImdbId);
            Assert.That(ids, Is.EqualTo(orderedIds));

            var url = Config.ListeningOn + "movies/search?take=100&orderBy=Rating,ImdbId";
            movies = url.AsJsonInto<Movie>();
            ids = movies.Results.Map(x => x.ImdbId);
            orderedIds = movies.Results.OrderBy(x => x.Rating).ThenBy(x => x.ImdbId).Map(x => x.ImdbId);
            Assert.That(ids, Is.EqualTo(orderedIds));

            url = Config.ListeningOn + "movies/search?take=100&orderByDesc=Rating,ImdbId";
            movies = url.AsJsonInto<Movie>();
            ids = movies.Results.Map(x => x.ImdbId);
            orderedIds = movies.Results.OrderByDescending(x => x.Rating)
                .ThenByDescending(x => x.ImdbId).Map(x => x.ImdbId);
            Assert.That(ids, Is.EqualTo(orderedIds));
        }

        [Test]
        public void Can_consume_as_CSV()
        {
            var url = Config.ListeningOn + "movies/search.csv?ratings=G,PG-13";
            var csv = url.GetStringFromUrl();
            var headers = csv.SplitOnFirst('\n')[0].Trim();
            Assert.That(headers, Is.EqualTo("Id,ImdbId,Title,Rating,Score,Director,ReleaseDate,TagLine,Genres"));
            csv.Print();

            url = Config.ListeningOn + "query/rockstars.csv?Age=27";
            csv = url.GetStringFromUrl();
            headers = csv.SplitOnFirst('\n')[0].Trim();
            Assert.That(headers, Is.EqualTo("Id,FirstName,LastName,Age,DateOfBirth,DateDied,LivingStatus"));
            csv.Print();

            url = Config.ListeningOn + "customrockstars.csv";
            csv = url.GetStringFromUrl();
            headers = csv.SplitOnFirst('\n')[0].Trim();
            Assert.That(headers, Is.EqualTo("FirstName,LastName,Age,RockstarAlbumName,RockstarGenreName"));
            csv.Print();
        }

        [Test]
        public void Does_not_query_Ignored_properties()
        {
            var response = client.Get(new QueryUnknownRockstars {
                UnknownProperty = "Foo",
                UnknownInt = 1,
            });

            Assert.That(response.Offset, Is.EqualTo(0));
            Assert.That(response.Total, Is.EqualTo(TotalRockstars));
            Assert.That(response.Results.Count, Is.EqualTo(TotalRockstars));
        }

        [Test]
        public void Can_Query_Rockstars_with_References()
        {
            var response = client.Get(new QueryRockstarsWithReferences {
                Age = 27
            });
         
            Assert.That(response.Results.Count, Is.EqualTo(3));

            var jimi = response.Results.First(x => x.FirstName == "Jimi");
            Assert.That(jimi.Albums.Count, Is.EqualTo(1));
            Assert.That(jimi.Albums[0].Name, Is.EqualTo("Electric Ladyland"));

            var jim = response.Results.First(x => x.FirstName == "Jim");
            Assert.That(jim.Albums, Is.Null);

            var kurt = response.Results.First(x => x.FirstName == "Kurt");
            Assert.That(kurt.Albums.Count, Is.EqualTo(1));
            Assert.That(kurt.Albums[0].Name, Is.EqualTo("Nevermind"));
        }

        [Test]
        public void Can_Query_AllFields_Guid()
        {
            var guid = new Guid("3EE6865A-4149-4940-B7A2-F952E0FEFC5E");
            var response = client.Get(new QueryAllFields {
                Guid = guid
            });

            Assert.That(response.Results.Count, Is.EqualTo(1));

            Assert.That(response.Results[0].Guid, Is.EqualTo(guid));
        }

        [Test]
        public void Does_populate_Total()
        {
            var response = client.Get(new QueryRockstars());
            Assert.That(response.Total, Is.EqualTo(response.Results.Count));
            Assert.That(response.Meta, Is.Null);

            response = client.Get(new QueryRockstars { Include = "COUNT" });
            Assert.That(response.Total, Is.EqualTo(response.Results.Count));

            response = client.Get(new QueryRockstars { Include = "COUNT(*)" });
            Assert.That(response.Total, Is.EqualTo(response.Results.Count));

            response = client.Get(new QueryRockstars { Include = "COUNT(DISTINCT LivingStatus)" });
            Assert.That(response.Total, Is.EqualTo(response.Results.Count));

            response = client.Get(new QueryRockstars { Include = "Count(*), Min(Age), Max(Age), Sum(Id)" });
            Assert.That(response.Total, Is.EqualTo(response.Results.Count));
        }

        [Test]
        public void Can_Include_Aggregates_in_AutoQuery()
        {
            var response = client.Get(new QueryRockstars { Include = "COUNT" });
            Assert.That(response.Meta["COUNT(*)"], Is.EqualTo(response.Results.Count.ToString()));

            response = client.Get(new QueryRockstars { Include = "COUNT(*)" });
            Assert.That(response.Meta["COUNT(*)"], Is.EqualTo(response.Results.Count.ToString()));

            response = client.Get(new QueryRockstars { Include = "COUNT(DISTINCT LivingStatus)" });
            Assert.That(response.Meta["COUNT(DISTINCT LivingStatus)"], Is.EqualTo("2"));

            response = client.Get(new QueryRockstars { Include = "MIN(Age)" });
            Assert.That(response.Meta["MIN(Age)"], Is.EqualTo(response.Results.Map(x => x.Age).Min().ToString()));

            response = client.Get(new QueryRockstars { Include = "Count(*), Min(Age), Max(Age), Sum(Id)" });
            Assert.That(response.Meta["Count(*)"], Is.EqualTo(response.Results.Count.ToString()));
            Assert.That(response.Meta["Min(Age)"], Is.EqualTo(response.Results.Map(x => x.Age).Min().ToString()));
            Assert.That(response.Meta["Max(Age)"], Is.EqualTo(response.Results.Map(x => x.Age).Max().ToString()));
            Assert.That(response.Meta["Sum(Id)"], Is.EqualTo(response.Results.Map(x => x.Id).Sum().ToString()));
        }

        [Test]
        public void Does_ignore_unknown_aggregate_commands()
        {
            var response = client.Get(new QueryRockstars { Include = "FOO(1)" });
            Assert.That(response.Total, Is.EqualTo(response.Results.Count));
            Assert.That(response.Meta, Is.Null);

            response = client.Get(new QueryRockstars { Include = "FOO(1), Min(Age), Bar('a') alias, Count(*), Baz(1,'foo')" });
            Assert.That(response.Total, Is.EqualTo(response.Results.Count));
            Assert.That(response.Meta["Min(Age)"], Is.EqualTo(response.Results.Map(x => x.Age).Min().ToString()));
            Assert.That(response.Meta["Count(*)"], Is.EqualTo(response.Results.Count.ToString()));
        }

        [Test]
        public void Can_Include_Aggregates_in_AutoQuery_with_Aliases()
        {
            var response = client.Get(new QueryRockstars { Include = "COUNT(*) Count" });
            Assert.That(response.Meta["Count"], Is.EqualTo(response.Results.Count.ToString()));

            response = client.Get(new QueryRockstars { Include = "COUNT(DISTINCT LivingStatus) as UniqueStatus" });
            Assert.That(response.Meta["UniqueStatus"], Is.EqualTo("2"));

            response = client.Get(new QueryRockstars { Include = "MIN(Age) MinAge" });
            Assert.That(response.Meta["MinAge"], Is.EqualTo(response.Results.Map(x => x.Age).Min().ToString()));

            response = client.Get(new QueryRockstars { Include = "Count(*) count, Min(Age) min, Max(Age) max, Sum(Id) sum" });
            Assert.That(response.Meta["count"], Is.EqualTo(response.Results.Count.ToString()));
            Assert.That(response.Meta["min"], Is.EqualTo(response.Results.Map(x => x.Age).Min().ToString()));
            Assert.That(response.Meta["max"], Is.EqualTo(response.Results.Map(x => x.Age).Max().ToString()));
            Assert.That(response.Meta["sum"], Is.EqualTo(response.Results.Map(x => x.Id).Sum().ToString()));
        }

        [Test]
        public void Can_execute_custom_aggregate_functions()
        {
            var response = client.Get(new QueryRockstars {
                Include = "ADD(6,2), Multiply(6,2) SixTimesTwo, Subtract(6,2), divide(6,2) TheDivide"
            });
            Assert.That(response.Meta["ADD(6,2)"], Is.EqualTo("8"));
            Assert.That(response.Meta["SixTimesTwo"], Is.EqualTo("12"));
            Assert.That(response.Meta["Subtract(6,2)"], Is.EqualTo("4"));
            Assert.That(response.Meta["TheDivide"], Is.EqualTo("3"));
        }
    }

    public static class AutoQueryExtensions
    {
        public static QueryResponse<T> AsJsonInto<T>(this string url)
        {
            return url.GetJsonFromUrl()
                .FromJson<QueryResponse<T>>();
        }
    }
}

