# LambdaMapper
This project started out as an exercise for me to gain a deeper expertise in metaprogramming using expression trees in C#.

`LambdaMapper` is yet another C# tool for mapping between various objects, for example, mapping between database entities and data transfer objects.

### Status
This is an advanced proof of concept. I have no plans at this time to continue development of this project.

### Usage
```
LambdaMapper.CreateMap<SourceClass, DestinationClass>();
LambdaMapper.CreateMap<SourceAddress, DestinationAddress>();
LambdaMapper.CreateMap<SourceRole, DestinationRole>();
LambdaMapper.InstantiateMapper();
var sourceClass = new SourceClass { ... }
var destinationClass = LambdaMapper.MapObject<SourceClass, DestinationClass>(sourceClass);
```

### Benchmarks
Using v.10 of `AutoMapper`, `LambdaMapper` had outperformed `AutoMapper`. After upgrading `AutoMapper` to v.11, `LambdaMapper` no longer outperforms `AutoMapper`.

|               Method |     Mean |     Error |    StdDev |  Gen 0 | Allocated |
|--------------------- |---------:|----------:|----------:|-------:|----------:|
|           Automapper | 1.879 us | 0.0225 us | 0.0221 us | 2.1725 |      4 KB |
|         LambdaMapper | 1.984 us | 0.0354 us | 0.0314 us | 2.6169 |      5 KB |
|   AutomapperWithNull | 1.049 us | 0.0191 us | 0.0178 us | 1.1463 |      2 KB |
| LambdaMapperWithNull | 1.228 us | 0.0142 us | 0.0133 us | 1.6518 |      3 KB |