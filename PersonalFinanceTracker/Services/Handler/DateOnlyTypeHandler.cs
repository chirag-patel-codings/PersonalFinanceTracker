using Dapper;
using MySql.Data.MySqlClient;
using System.Data;

namespace PersonalFinanceTracker.Services.Handler
{
    /// <summary>
    ///  This class tells Dapper how to convert DateOnly.
    /// </summary>
    public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            /*
            parameter.DbType = DbType.Date; // Force correct SQL type
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
            */

            var p = (MySqlParameter)parameter;
            p.MySqlDbType = MySqlDbType.Date;
            p.Value = value.ToDateTime(TimeOnly.MinValue);
        }

        public override DateOnly Parse(object value)
        {
            if (value is DBNull) return default;
            return DateOnly.FromDateTime((DateTime)value);
        }
    }
}
