// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class SqlServerDateOnlyMethodTranslator : IMethodCallTranslator
{
    private static readonly MethodInfo DateOnlyToDateTimeMethod = typeof(DateOnly).GetRuntimeMethod(
        nameof(DateOnly.ToDateTime), [typeof(TimeOnly)])!;

    private readonly Dictionary<MethodInfo, string> _methodInfoDatePartMapping = new()
    {
        { typeof(DateOnly).GetRuntimeMethod(nameof(DateOnly.AddYears), [typeof(int)])!, "year" },
        { typeof(DateOnly).GetRuntimeMethod(nameof(DateOnly.AddMonths), [typeof(int)])!, "month" },
        { typeof(DateOnly).GetRuntimeMethod(nameof(DateOnly.AddDays), [typeof(int)])!, "day" }
    };

    private readonly ISqlExpressionFactory _sqlExpressionFactory;
    private readonly IRelationalTypeMappingSource _typeMappingSource;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public SqlServerDateOnlyMethodTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _typeMappingSource = typeMappingSource;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance != null)
        {
            if (_methodInfoDatePartMapping.TryGetValue(method, out var datePart))
            {
                instance = _sqlExpressionFactory.ApplyDefaultTypeMapping(instance);

                return _sqlExpressionFactory.Function(
                    "DATEADD",
                    new[] { _sqlExpressionFactory.Fragment(datePart), _sqlExpressionFactory.Convert(arguments[0], typeof(int)), instance },
                    nullable: true,
                    argumentsPropagateNullability: new[] { false, true, true },
                    instance.Type,
                    instance.TypeMapping);
            }

            if (DateOnlyToDateTimeMethod == method && arguments.Count == 1)
            {
                instance = _sqlExpressionFactory.ApplyDefaultTypeMapping(instance);

                var yearPart = DatePart("year", instance, typeof(int));
                var monthPart = DatePart("month", instance, typeof(int));
                var dayPart = DatePart("day", instance, typeof(int));
                var hourPart = DatePart("hour", arguments[0], typeof(int));
                var minutePart = DatePart("minute", arguments[0], typeof(int));
                var secondPart = DatePart("second", arguments[0], typeof(int));
                var fractionsPart = _sqlExpressionFactory.Constant(0, typeof(int));
                var precisionPart = _sqlExpressionFactory.Constant(0, typeof(int));

                var func = _sqlExpressionFactory.Function(
                    "DATETIME2FROMPARTS",
                    new[] { yearPart, monthPart, dayPart, hourPart, minutePart, secondPart, fractionsPart, precisionPart },
                    nullable: true,
                    argumentsPropagateNullability: new[] { true, true, true, true, true, true, true, true },
                    method.ReturnType,
                    _typeMappingSource.FindMapping(method.ReturnType, "datetime")
                );

                return func;

            }
        }


        if (method.DeclaringType == typeof(DateOnly)
            && method.Name == nameof(DateOnly.FromDateTime)
            && arguments.Count == 1)
        {
            return _sqlExpressionFactory.Convert(arguments[0], typeof(DateOnly));
        }

        return null;
    }

    private SqlExpression DatePart(string part, SqlExpression instance, Type returnType) => _sqlExpressionFactory.Function(
        "DATEPART",
        arguments: [_sqlExpressionFactory.Fragment(part), instance],
        nullable: true,
        argumentsPropagateNullability: Statics.FalseTrue,
        returnType);
}
