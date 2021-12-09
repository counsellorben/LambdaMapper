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
Much to my surprise, `LambdaMapper` outperforms `AutoMapper`, despite the fact that `LambdaMapper` is creating clones of any objects in the source object graph which are of the same type, while `AutoMapper` merely uses the same object in the newly created object graph.

|               Method |     Mean |     Error |    StdDev |
|--------------------- |---------:|----------:|----------:|
|           Automapper | 2.483 us | 0.0182 us | 0.0170 us |
|         LambdaMapper | 1.770 us | 0.0116 us | 0.0108 us |
|   AutomapperWithNull | 1.241 us | 0.0112 us | 0.0105 us |
| LambdaMapperWithNull | 1.075 us | 0.0119 us | 0.0112 us |
