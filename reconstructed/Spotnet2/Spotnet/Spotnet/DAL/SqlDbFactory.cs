using Spotnet.Helpers;

namespace Spotnet.DAL;

public static class SqlDbFactory
{
	private static ISqlDb CreateSqlDb(string connectionString, bool isReadOnly = false)
	{
		isReadOnly = false;
		return new SQliteDb(connectionString, isReadOnly);
	}

	public static ISqlDb CreateSqlDbSpots(bool isReadOnly = false)
	{
		return CreateSqlDb(AppHelper.GetDbFilename("dbs"), isReadOnly);
	}

	public static ISqlDb CreateSqlDbNewznabSpots(bool isReadOnly = false)
	{
		return CreateSqlDb(AppHelper.GetDbFilename("newznab.dbs"), isReadOnly);
	}

	public static ISqlDb CreateSqlDbComments(bool isReadOnly = false)
	{
		return CreateSqlDb(AppHelper.GetDbFilename("dbc"), isReadOnly);
	}
}
