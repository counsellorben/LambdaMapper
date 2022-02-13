using System;
using System.Collections.Generic;
using AutoMapper;
using AutoMapper.Extensions.EnumMapping;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Newtonsoft.Json.Serialization;

namespace LambdaMapper.Benchmarks
{
    [MemoryDiagnoser]
    public class Benchmarks
    {
        private IMapper _mapper;

        [GlobalSetup]
        public void Setup()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Operation<SourceAddress>, Operation<DestinationAddress>>();
                cfg.CreateMap<JsonPatchDocument<SourceAddress>, JsonPatchDocument<DestinationAddress>>();
                // cfg.CreateMap<SourceName, DestinationName>();
                cfg.CreateMap<SourceAddress, DestinationAddress>();
                cfg.CreateMap<SourceRole, DestinationRole>();
                cfg.CreateMap<(SourceAddress, SourceAddress), (DestinationAddress, DestinationAddress)>();
                cfg.CreateMap<SourceClass, DestinationClass>();
                cfg.CreateMap<SourceEnum, DestinationEnum>()
                    .ConvertUsingEnumMapping(opt =>
                        opt.MapByName());
            });
            _mapper = config.CreateMapper();

            LambdaMapper.CreateMap<Operation<SourceAddress>, Operation<DestinationAddress>>();
            LambdaMapper.CreateMap<JsonPatchDocument<SourceAddress>, JsonPatchDocument<DestinationAddress>>();
            // LambdaMapper.CreateMap<SourceName, DestinationName>();
            LambdaMapper.CreateMap<SourceAddress, DestinationAddress>();
            LambdaMapper.CreateMap<SourceRole, DestinationRole>();
            LambdaMapper.CreateMap<SourceClass, DestinationClass>();
            LambdaMapper.CreateEnumMap<SourceEnum, DestinationEnum>();
            LambdaMapper.InstantiateMapper();
        }

        [Benchmark]
        public void Automapper()
        {
            var sourceClass = GetSourceClass();
            var destinationClass = _mapper.Map<SourceClass, DestinationClass>(sourceClass);
        }

        [Benchmark(Description = "LambdaMapper")]
        public void LambdaMapperBenchmarks()
        {
            var sourceClass = GetSourceClass();
            var destinationClass = LambdaMapper.MapObject<SourceClass, DestinationClass>(sourceClass);
        }

        [Benchmark]
        public void AutomapperWithNull()
        {
            var sourceClass = GetSourceClassWithNull();
            var destinationClass = _mapper.Map<SourceClass, DestinationClass>(sourceClass);
        }

        [Benchmark]
        public void LambdaMapperWithNull()
        {
            var sourceClass = GetSourceClassWithNull();
            var destinationClass = LambdaMapper.MapObject<SourceClass, DestinationClass>(sourceClass);
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
                        AddressLine = "121 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    },
                    new SourceAddress
                    {
                        AddressLine = "122 Main Street",
                        AddressLine2 = "#2B",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    },
                    new SourceAddress
                    {
                        AddressLine = "123 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    },
                    new SourceAddress
                    {
                        AddressLine = "124 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    },
                    new SourceAddress
                    {
                        AddressLine = "125 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    },
                    new SourceAddress
                    {
                        AddressLine = "126 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    },
                    new SourceAddress
                    {
                        AddressLine = "127 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    },
                    new SourceAddress
                    {
                        AddressLine = "128 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    },
                    new SourceAddress
                    {
                        AddressLine = "129 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    },
                    new SourceAddress
                    {
                        AddressLine = "130 Main Street",
                        City = "Peoria",
                        State = "Illinois",
                        PostalCode = "61525"
                    },
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
                // FullName = new SourceName("Buckaroo", "Banzai"),
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
                // FullName = new SourceName("Buckaroo", "Banzai"),
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
            // public SourceAddress? PrimaryAddress { get; set; }
            public SourceAddress PrimaryAddress { get; set; }
            public IEnumerable<SourceAddress> Addresses { get; set; }
            public DateTime Created { get; set; }
            public Dictionary<int, SourceRole> Roles { get; set; }
            public (SourceAddress address1, SourceAddress address2) TupleAddresses { get; set; }
            // public SourceName FullName { get; init; }
            public JsonPatchDocument<SourceAddress> AddressChange { get; set; }
            public SourceEnum EnumValue { get; set; }
        }

        public class DestinationClass
        {
            public Guid Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            // public SourceAddress? PrimaryAddress { get; set; }
            // public DestinationAddress? PrimaryAddress { get; set; }
            public DestinationAddress PrimaryAddress { get; set; }
            public IEnumerable<DestinationAddress> Addresses { get; set; }

            // public Dictionary<int, SourceRole> Roles { get; set; }
            public Dictionary<int, DestinationRole> Roles { get; set; }

            // public (SourceAddress address1, SourceAddress address2) TupleAddresses { get; set; }
            public (DestinationAddress address1, DestinationAddress address2) TupleAddresses { get; set; }

            // public SourceName FullName { get; init; }
            // public DestinationName FullName { get; init; }
            public JsonPatchDocument<DestinationAddress> AddressChange { get; set; }
            public DestinationEnum EnumValue { get; set; }
        }

        public enum SourceEnum
        {
            First,
            Second,
            Third
        }

        public enum DestinationEnum
        {
            Fourth,
            Third,
            Second,
            First
        }

        // public record SourceName(string FirstName, string LastName) {};

        // public record DestinationName(string FirstName, string LastName) {};

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