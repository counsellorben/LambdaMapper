using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AutoMapper;
using HigLabo.Core;
using Mapster;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace LambdaMapper.Tests
{
    public class MapperBenchmarkTests
    {
        [Test]
        public void FastExpressionCompilerTest()
        {
            LambdaMapper.CreateMap<Source, Dest>();
            LambdaMapper.CreateMap<SourceAddress, DestinationAddress>();
            LambdaMapper.CreateMap<SourceName, DestinationName>();
            LambdaMapper.CreateMap<SourceRole, DestinationRole>();
            LambdaMapper.InstantiateMapper();
            var srcWithNull = GetSourceWithNull();
            var destWithNull = LambdaMapper.MapObject<Source, Dest>(srcWithNull);
            Assert.AreEqual(srcWithNull.Id, destWithNull.Id);

            var src = GetSource();
            var dest = LambdaMapper.MapObject<Source, Dest>(src);
            Assert.AreEqual(src.Id, dest.Id);
        }

        [Test]
        public void LambdaMapperWithAndWithoutNull()
        {
            LambdaMapper.CreateMap<JsonPatchDocument<SourceAddress>, JsonPatchDocument<DestinationAddress>>();
            LambdaMapper.CreateMap<Operation<SourceAddress>, Operation<DestinationAddress>>();
            LambdaMapper.CreateMap<SourceClass, DestinationClass>();
            LambdaMapper.CreateMap<SourceAddress, DestinationAddress>();
            LambdaMapper.CreateMap<SourceRole, DestinationRole>();
            LambdaMapper.CreateMap<SourceName, DestinationName>();
            LambdaMapper.InstantiateMapper();
            var sourceClassWithNull = GetSourceClassWithNull();
            var destinationClassWithNull = LambdaMapper.MapObject<SourceClass, DestinationClass>(sourceClassWithNull);
            Assert.AreEqual(sourceClassWithNull.Id, destinationClassWithNull.Id);
            Assert.AreEqual(sourceClassWithNull.FullName.LastName, destinationClassWithNull.FullName.LastName);
            Assert.AreEqual(sourceClassWithNull.FirstName, destinationClassWithNull.FirstName);
            Assert.IsNull(sourceClassWithNull.PrimaryAddress);
            Assert.IsNull(destinationClassWithNull.PrimaryAddress);
            Assert.AreEqual(sourceClassWithNull.LastName, destinationClassWithNull.LastName);
            Assert.AreEqual(sourceClassWithNull.Addresses.First().AddressLine, destinationClassWithNull.Addresses.First().AddressLine);
            Assert.AreEqual(
                sourceClassWithNull.Roles.First().Value.RoleName,
                destinationClassWithNull.Roles.First().Value.RoleName);
            Assert.AreEqual(
                sourceClassWithNull.AddressChange.Operations.First().value,
                destinationClassWithNull.AddressChange.Operations.First().value);

            var sourceClass = GetSourceClass();
            var destinationClass = LambdaMapper.MapObject<SourceClass, DestinationClass>(sourceClass);
            Assert.AreEqual(sourceClass.Id, destinationClass.Id);
            Assert.AreEqual(sourceClass.FullName.LastName, destinationClass.FullName.LastName);
            Assert.AreEqual(sourceClass.FirstName, destinationClass.FirstName);
            Assert.AreEqual(sourceClass.PrimaryAddress.AddressLine, destinationClass.PrimaryAddress.AddressLine);
            Assert.AreEqual(sourceClass.LastName, destinationClass.LastName);
            Assert.AreEqual(sourceClass.Addresses.First().AddressLine, destinationClass.Addresses.First().AddressLine);
            Assert.AreEqual(sourceClass.Roles.First().Value.RoleName, destinationClass.Roles.First().Value.RoleName);
            Assert.AreEqual(
                sourceClass.TupleAddresses.address1.AddressLine,
                destinationClass.TupleAddresses.address1.AddressLine);
            Assert.AreEqual(
                sourceClass.AddressChange.Operations.First().value,
                destinationClass.AddressChange.Operations.First().value);
        }

        [Test]
        public void MapsterMapperTest()
        {
            var config = new TypeAdapterConfig();
            config.NewConfig<SourceName, SourceName>()
                .ConstructUsing(s => new SourceName(s.FirstName, s.LastName))
                .IgnoreNullValues(true);
            config.NewConfig<SourceName, DestinationName>()
                .ConstructUsing(s => new DestinationName(s.FirstName, s.LastName))
                .IgnoreNullValues(true);
            config.NewConfig<IContractResolver, IContractResolver>()
                .ConstructUsing(s => s)
                .IgnoreNullValues(true);
            config.NewConfig<SourceAddress, DestinationAddress>()
                .IgnoreNullValues(true);
            config.NewConfig<(SourceAddress, SourceAddress), (DestinationAddress, DestinationAddress)>()
                .IgnoreNullValues(true);
            config.Compile();

            var sourceClassWithNull = GetSourceClassWithNull();
            var destinationClassWithNull = sourceClassWithNull.Adapt<DestinationClass>(config);
            Assert.AreEqual(sourceClassWithNull.Id, destinationClassWithNull.Id);
            Assert.AreEqual(sourceClassWithNull.FullName.LastName, destinationClassWithNull.FullName.LastName);
            Assert.AreEqual(sourceClassWithNull.FirstName, destinationClassWithNull.FirstName);
            Assert.IsNull(sourceClassWithNull.PrimaryAddress);
            Assert.IsNull(destinationClassWithNull.PrimaryAddress);
            Assert.AreEqual(sourceClassWithNull.LastName, destinationClassWithNull.LastName);
            Assert.AreEqual(sourceClassWithNull.Addresses.First().AddressLine, destinationClassWithNull.Addresses.First().AddressLine);
            Assert.AreEqual(
                sourceClassWithNull.Roles.First().Value.RoleName,
                destinationClassWithNull.Roles.First().Value.RoleName);
            // Assert.AreEqual(
            //     sourceClassWithNull.AddressChange.Operations.First().value,
            //     destinationClassWithNull.AddressChange.Operations.First().value);

            var sourceClass = GetSourceClass();
            var destinationClass = sourceClass.Adapt<DestinationClass>(config);
            Assert.AreEqual(sourceClass.Id, destinationClass.Id);
            Assert.AreEqual(sourceClass.FullName.LastName, destinationClass.FullName.LastName);
            Assert.AreEqual(sourceClass.FirstName, destinationClass.FirstName);
            Assert.AreEqual(sourceClass.PrimaryAddress.AddressLine, destinationClass.PrimaryAddress.AddressLine);
            Assert.AreEqual(sourceClass.LastName, destinationClass.LastName);
            Assert.AreEqual(sourceClass.Addresses.First().AddressLine, destinationClass.Addresses.First().AddressLine);
            Assert.AreEqual(sourceClass.Roles.First().Value.RoleName, destinationClass.Roles.First().Value.RoleName);
            Assert.AreEqual(
                sourceClass.TupleAddresses.address1.AddressLine,
                destinationClass.TupleAddresses.address1.AddressLine);
            // Assert.AreEqual(
            //     sourceClass.AddressChange.Operations.First().value,
            //     destinationClass.AddressChange.Operations.First().value);
        }

        [Test]
        public void SourceClass_maps_to_DestinationClass()
        {
            Console.WriteLine("Starting test");
            var stopwatch = new Stopwatch();
            const int NUMBER_OF_ITERATIONS = 1000000;

            // Compiled expression tree mapper
            stopwatch.Reset();
            stopwatch.Start();
            LambdaMapper.CreateMap<JsonPatchDocument<SourceAddress>, JsonPatchDocument<DestinationAddress>>();
            LambdaMapper.CreateMap<Operation<SourceAddress>, Operation<DestinationAddress>>();
            LambdaMapper.CreateMap<SourceClass, DestinationClass>();
            LambdaMapper.CreateMap<SourceAddress, DestinationAddress>();
            LambdaMapper.CreateMap<SourceRole, DestinationRole>();
            LambdaMapper.CreateMap<SourceName, DestinationName>();
            LambdaMapper.InstantiateMapper();
            stopwatch.Stop();
            Console.WriteLine(
                $"LambdaMapper setup took {stopwatch.ElapsedMilliseconds:#,###} milliseconds");

            stopwatch.Reset();
            stopwatch.Start();
            for (var i = 0; i < NUMBER_OF_ITERATIONS; i++)
            {
                var sourceClass = GetSourceClass();
                var destinationClass = LambdaMapper.MapObject<SourceClass, DestinationClass>(sourceClass);
            }
            stopwatch.Stop();
            Console.WriteLine(
                $"{NUMBER_OF_ITERATIONS:#,###} compiled expression tree iterations took {stopwatch.ElapsedMilliseconds:#,###} milliseconds");

            // Automapper
            stopwatch.Reset();
            stopwatch.Start();
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Operation<SourceAddress>, Operation<DestinationAddress>>();
                cfg.CreateMap<JsonPatchDocument<SourceAddress>, JsonPatchDocument<DestinationAddress>>();
                cfg.CreateMap<SourceClass, DestinationClass>();
                cfg.CreateMap<SourceAddress, DestinationAddress>();
                cfg.CreateMap<(SourceAddress, SourceAddress), (SourceAddress, SourceAddress)>();
                cfg.CreateMap<(SourceAddress, SourceAddress), (DestinationAddress, DestinationAddress)>();
                cfg.CreateMap<SourceRole, DestinationRole>();
                cfg.CreateMap<SourceName, DestinationName>();
            });
            var mapper = config.CreateMapper();
            stopwatch.Stop();
            Console.WriteLine(
                $"Automapper setup took {stopwatch.ElapsedMilliseconds:#,###} milliseconds");

            stopwatch.Reset();
            stopwatch.Start();
            for (var i = 0; i < NUMBER_OF_ITERATIONS; i++)
            {
                var sourceClass = GetSourceClass();
                var destinationClass = mapper.Map<SourceClass, DestinationClass>(sourceClass);
            }
            stopwatch.Stop();
            Console.WriteLine(
                $"{NUMBER_OF_ITERATIONS:#,###} Automapper iterations took {stopwatch.ElapsedMilliseconds:#,###} milliseconds");
        }

        private Source GetSource() =>
            new Source
            {
                Id = Guid.NewGuid(),
                FirstName = "Buckaroo",
                LastName = "Banzai",
                PrimaryAddress = new SourceAddress
                {
                    AddressLine = "999 Pecan Street",
                    City = "Peoria",
                    State = "Illinois",
                    PostalCode = "61525"
                },
                Created = DateTime.UtcNow,
                FullName = new SourceName("Buckaroo", "Banzai") { MiddleName = "Alan" },
                Addresses = new List<SourceAddress>
                {
                    new SourceAddress
                    {
                        AddressLine = "123 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    }
                },
                Roles = new Dictionary<int, SourceRole>
                {
                    { 0, new SourceRole { RoleName = "Ruler of all" } }
                },
            };

        private Source GetSourceWithNull() =>
            new Source
            {
                Id = Guid.NewGuid(),
                FirstName = "Buckaroo",
                LastName = "Banzai",
                Created = DateTime.UtcNow,
                FullName = new SourceName("Buckaroo", "Banzai") { MiddleName = "Alan" },
                Addresses = new List<SourceAddress>
                {
                    new SourceAddress
                    {
                        AddressLine = "123 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    }
                },
                Roles = new Dictionary<int, SourceRole>
                {
                    { 0, new SourceRole { RoleName = "Ruler of all" } }
                },
            };

        public class Source
        {
            public Guid Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            #nullable enable
            public SourceAddress? PrimaryAddress { get; set; }
            #nullable disable
            public IEnumerable<SourceAddress> Addresses { get; set; }
            public DateTime Created { get; set; }
            public SourceName FullName { get; init; }
            public Dictionary<int, SourceRole> Roles { get; set; }
        }

        public class Dest
        {
            public Guid Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            #nullable enable
            public DestinationAddress? PrimaryAddress { get; set; }
            #nullable disable
            public IEnumerable<DestinationAddress> Addresses { get; set; }
            public DateTime Created { get; set; }
            public DestinationName FullName { get; init; }
            public Dictionary<int, DestinationRole> Roles { get; set; }
        }

        private SourceClass GetSourceClass() =>
            new SourceClass
            {
                Id = Guid.NewGuid(),
                FirstName = "Buckaroo",
                LastName = "Banzai",
                PrimaryAddress = new SourceAddress
                {
                    AddressLine = "999 Pecan Street",
                    City = "Peoria",
                    State = "Illinois",
                    PostalCode = "61525"
                },
                Addresses = new List<SourceAddress>
                {
                    new SourceAddress
                    {
                        AddressLine = "123 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    }
                },
                Roles = new Dictionary<int, SourceRole>
                {
                    { 0, new SourceRole { RoleName = "Ruler of all" } }
                },
                TupleAddresses = (
                    new SourceAddress
                    {
                        AddressLine = "11 South Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    },
                    new SourceAddress
                    {
                        AddressLine = "999 North Avenue",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    }
                ),
                Created = DateTime.UtcNow,
                FullName = new SourceName("Buckaroo", "Banzai") { MiddleName = "Alan" },
                AddressChange = new JsonPatchDocument<SourceAddress>(
                    new List<Operation<SourceAddress>>
                    {
                        new Operation<SourceAddress>
                        {
                            op = "replace",
                            path = $"/{nameof(SourceAddress.AddressLine)}",
                            value = "a new address"
                        }
                    },
                    new DefaultContractResolver())
            };

        private SourceClass GetSourceClassWithNull() =>
            new SourceClass
            {
                Id = Guid.NewGuid(),
                FirstName = "Buckaroo",
                LastName = "Banzai",
                Addresses = new List<SourceAddress>
                {
                    new SourceAddress
                    {
                        AddressLine = "123 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    }
                },
                Roles = new Dictionary<int, SourceRole>
                {
                    { 0, new SourceRole { RoleName = "Ruler of all" } }
                },
                Created = DateTime.UtcNow,
                FullName = new SourceName("Buckaroo", "Banzai") { MiddleName = "Alan" },
                AddressChange = new JsonPatchDocument<SourceAddress>(
                    new List<Operation<SourceAddress>>
                    {
                        new Operation<SourceAddress>
                        {
                            op = "replace",
                            path = $"/{nameof(SourceAddress.AddressLine)}",
                            value = "a new address"
                        }
                    },
                    new DefaultContractResolver())
            };

        public class SourceClass
        {
            public Guid Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            #nullable enable
            public SourceAddress? PrimaryAddress { get; set; }
            #nullable disable
            public IEnumerable<SourceAddress> Addresses { get; set; }
            public DateTime Created { get; set; }
            public Dictionary<int, SourceRole> Roles { get; set; }
            public (SourceAddress address1, SourceAddress address2) TupleAddresses { get; set; }
            public SourceName FullName { get; init; }
            public JsonPatchDocument<SourceAddress> AddressChange { get; set; }
        }

        public class DestinationClass
        {
            public Guid Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            #nullable enable
            public DestinationAddress? PrimaryAddress { get; set; }
            #nullable disable
            public IEnumerable<DestinationAddress> Addresses { get; set; }

            // public Dictionary<int, SourceRole> Roles { get; set; }
            public Dictionary<int, DestinationRole> Roles { get; set; }

            // public (SourceAddress address1, SourceAddress address2) TupleAddresses { get; set; }
            // public (DestinationAddress address1, SourceAddress address2) TupleAddresses { get; set; }
            public (DestinationAddress address1, DestinationAddress address2) TupleAddresses { get; set; }

            public SourceName FullName { get; init; }
            // public DestinationName FullName { get; init; }

            public JsonPatchDocument<DestinationAddress> AddressChange { get; set; }
        }

        public record SourceName(string FirstName, string LastName)
        {
            #nullable enable
            public string? MiddleName { get; init; }
            #nullable disable
        };

        public record DestinationName(string FirstName, string LastName)
        {
            #nullable enable
            public string? MiddleName { get; set; }
            #nullable disable
        };

        public class SourceAddress
        {
            public string AddressLine { get; set; }
            public string AddressLine2 { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string PostalCode { get; set; }
            public DateTime Created { get; set; }
       }

        public class DestinationAddress
        {
            public string AddressLine { get; set; }
            public string AddressLine2 { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string PostalCode { get; set; }
            public DateTime Created { get; set; }
       }

       public class SourceRole
       {
           public int Id { get; set; }
           public string RoleName { get; set; }
       }

       public class DestinationRole
       {
           public string RoleName { get; set; }
       }
    }
}