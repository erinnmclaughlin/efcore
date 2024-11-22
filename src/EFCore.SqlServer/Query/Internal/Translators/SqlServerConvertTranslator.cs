// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class SqlServerConvertTranslator : IMethodCallTranslator
{
    private static readonly Dictionary<string, string> TypeMapping = new()
    {
        [nameof(Convert.ToBoolean)] = "bit",
        [nameof(Convert.ToByte)] = "tinyint",
        [nameof(Convert.ToDecimal)] = "decimal(18, 2)",
        [nameof(Convert.ToDouble)] = "float",
        [nameof(Convert.ToInt16)] = "smallint",
        [nameof(Convert.ToInt32)] = "int",
        [nameof(Convert.ToInt64)] = "bigint",
        [nameof(Convert.ToString)] = "nvarchar(max)",
        [nameof(Convert.ToDateTime)] = "datetime2"
    };

    private static readonly List<Type> SupportedTypes =
    [
        typeof(bool),
        typeof(byte),
        typeof(DateTime),
        typeof(decimal),
        typeof(double),
        typeof(float),
        typeof(int),
        typeof(long),
        typeof(short),
        typeof(string),
        typeof(object)
    ];

    private static readonly MethodInfo[] SupportedMethods
        = TypeMapping.Keys
            .SelectMany(
                t => typeof(Convert).GetTypeInfo().GetDeclaredMethods(t)
                    .Where(
                        m => m.GetParameters().Length == 1
                            && SupportedTypes.Contains(m.GetParameters().First().ParameterType)))
            .ToArray();

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public SqlServerConvertTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

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
        if (!SupportedMethods.Contains(method))
        {
            return null;
        }

        var toConvert = arguments[0];

        if (method.Name == nameof(Convert.ToDateTime) && arguments[0].Type != typeof(string))
        {
            toConvert = _sqlExpressionFactory.Convert(arguments[0], typeof(string));
        }

        return _sqlExpressionFactory.Function(
            "CONVERT",
            [_sqlExpressionFactory.Fragment(TypeMapping[method.Name]), toConvert],
            nullable: true,
            argumentsPropagateNullability: Statics.FalseTrue,
            method.ReturnType
        );
    }
}
