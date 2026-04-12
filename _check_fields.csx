using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Storage;

var fields = typeof(RelationalConnection)
    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
foreach (var f in fields)
    Console.WriteLine($"{f.Name} : {f.FieldType.Name}");
