using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using AutoMapper.Extensions.EnumMapping;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace LambdaMapper.Tests
{
    public class MapperBenchmarkTests
    {
        [Test]
        public void LambdaMapperWithAndWithoutNull()
        {
            LambdaMapper.CreateMap<JsonPatchDocument<SourceAddress>, JsonPatchDocument<DestinationAddress>>();
            LambdaMapper.CreateMap<Operation<SourceAddress>, Operation<DestinationAddress>>();
            LambdaMapper.CreateMap<SourceClass, DestinationClass>();
            LambdaMapper.CreateMap<SourceAddress, DestinationAddress>();
            LambdaMapper.CreateMap<SourceRole, DestinationRole>();
            LambdaMapper.CreateEnumMap<SourceEnum, DestinationEnum>();
            LambdaMapper.InstantiateMapper();
            var sourceClassWithNull = GetSourceClassWithNull();
            var destinationClassWithNull = LambdaMapper.MapObject<SourceClass, DestinationClass>(sourceClassWithNull);
            Assert.AreEqual(sourceClassWithNull.Id, destinationClassWithNull.Id);
            Assert.AreEqual(sourceClassWithNull.FirstName, destinationClassWithNull.FirstName);
            Assert.IsNull(sourceClassWithNull.PrimaryAddress);
            Assert.IsNull(destinationClassWithNull.PrimaryAddress);
            Assert.AreEqual(sourceClassWithNull.LastName, destinationClassWithNull.LastName);
            Assert.AreEqual(
                sourceClassWithNull.EnumValue.ToString(),
                destinationClassWithNull.EnumValue.ToString());

            var sourceClass = GetSourceClass();
            var destinationClass = LambdaMapper.MapObject<SourceClass, DestinationClass>(sourceClass);
            Assert.AreEqual(sourceClass.Id, destinationClass.Id);
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
            Assert.AreEqual(
                sourceClassWithNull.EnumValue.ToString(),
                destinationClassWithNull.EnumValue.ToString());
        }

        [Test]
        public void AutoMapperWithAndWithoutNull()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Operation<SourceAddress>, Operation<DestinationAddress>>();
                cfg.CreateMap<JsonPatchDocument<SourceAddress>, JsonPatchDocument<DestinationAddress>>();
                cfg.CreateMap<SourceAddress, DestinationAddress>();
                cfg.CreateMap<SourceRole, DestinationRole>();
                cfg.CreateMap<(SourceAddress, SourceAddress, bool), (DestinationAddress, DestinationAddress)>();
                cfg.CreateMap<SourceClass, DestinationClass>();
                cfg.CreateMap<SourceEnum, DestinationEnum>()
                    .ConvertUsingEnumMapping(opt =>
                        opt.MapByName());
            });
            var mapper = config.CreateMapper();

            var sourceClassWithNull = GetSourceClassWithNull();
            var destinationClassWithNull = mapper.Map<SourceClass, DestinationClass>(sourceClassWithNull);
            Assert.AreEqual(sourceClassWithNull.Id, destinationClassWithNull.Id);
            Assert.AreEqual(sourceClassWithNull.FirstName, destinationClassWithNull.FirstName);
            Assert.IsNull(sourceClassWithNull.PrimaryAddress);
            Assert.IsNull(destinationClassWithNull.PrimaryAddress);
            Assert.AreEqual(sourceClassWithNull.LastName, destinationClassWithNull.LastName);
            Assert.AreEqual(
                sourceClassWithNull.EnumValue.ToString(),
                destinationClassWithNull.EnumValue.ToString());

            var sourceClass = GetSourceClass();
            var destinationClass = mapper.Map<SourceClass, DestinationClass>(sourceClass);
            Assert.AreEqual(sourceClass.Id, destinationClass.Id);
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
            Assert.AreEqual(
                sourceClassWithNull.EnumValue.ToString(),
                destinationClassWithNull.EnumValue.ToString());
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
                    },
                    true
                ),
                Created = DateTime.UtcNow,
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
                    new DefaultContractResolver()),
                EnumValue = SourceEnum.Second
            };

        private SourceClass GetSourceClassWithNull() =>
            new SourceClass
            {
                FirstName = "Buckaroo",
                LastName = "Banzai",
                Created = DateTime.UtcNow,
                // SourceEnum = SourceEnum.Second
            };

        public class SourceClass
        {
            public Guid? Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            // public SourceAddress? PrimaryAddress { get; set; }
            public SourceAddress PrimaryAddress { get; set; }
            public IEnumerable<SourceAddress> Addresses { get; set; }
            public DateTime Created { get; set; }
            public Dictionary<int, SourceRole> Roles { get; set; }
            public (SourceAddress address1, SourceAddress address2, bool moreAddresses) TupleAddresses { get; set; }
            // public SourceName FullName { get; init; }
            public JsonPatchDocument<SourceAddress> AddressChange { get; set; }
            public SourceEnum EnumValue { get; set; }
        }

        public class DestinationClass
        {
            public Guid? Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            // public DestinationAddress? PrimaryAddress { get; set; }
            public DestinationAddress PrimaryAddress { get; set; }
            public IEnumerable<DestinationAddress> Addresses { get; set; }

            // public Dictionary<int, SourceRole> Roles { get; set; }
            public Dictionary<int, DestinationRole> Roles { get; set; }

            // public (SourceAddress address1, SourceAddress address2) TupleAddresses { get; set; }
            // public (DestinationAddress address1, SourceAddress address2) TupleAddresses { get; set; }
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

        // public record SourceName(string FirstName, string LastName)
        // {
        //     public string? MiddleName { get; init; }
        // };

        // public record DestinationName(string FirstName, string LastName)
        // {
        //     public string? MiddleName { get; set; }
        // };

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