using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Spotnet.Model;

public sealed class ObjectBinder : SerializationBinder
{
	public override Type BindToType(string assemblyName, string typeName)
	{
		assemblyName = Assembly.GetExecutingAssembly().FullName;
		return Type.GetType($"{typeName}, {assemblyName}");
	}
}
