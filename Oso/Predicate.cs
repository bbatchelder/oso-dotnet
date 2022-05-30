namespace Oso;

public record class Predicate(string Name, IEnumerable<object?> Arguments)
{
    bool IEquatable<Predicate>.Equals(Predicate? other)
    {
        return other != null && Name == other.Name && Arguments.SequenceEqual(other.Arguments);

    }

    public override int GetHashCode() => Name.GetHashCode() * 17 + Arguments.GetHashCode();
};