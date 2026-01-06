using System.Linq.Expressions;

namespace application.Models;

public interface IPlotReference
{
    string Name { get; }
    string Unit { get; }
    double GetValue();
    string PropertyName { get; }
    object? SourceObject { get; }
}

public class PlotReference<TSource> : IPlotReference
{
    private readonly TSource _source;
    private readonly Func<TSource, double> _getter;

    public PlotReference(
        TSource source, 
        Expression<Func<TSource, double>> propertyExpression,
        string name,
        string unit)
    {
        _source = source;
        _getter = propertyExpression.Compile();
        Name = name;
        Unit = unit;

        PropertyName = "";
        if (propertyExpression.Body is MemberExpression memberExpr)
        {
            PropertyName = memberExpr.Member.Name;
        }
    }

    public string Name { get; }
    public string Unit { get; }
    public string PropertyName { get; }
    public object? SourceObject => _source;
    
    public double GetValue() => _getter(_source);
}