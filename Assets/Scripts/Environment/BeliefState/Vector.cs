using System;
using UnityEngine;

public readonly struct Vector
{
    private readonly float[] _values;

    public int DimensionCount => _values.Length;

    public float this[int dimension] => _values[dimension];

    public Vector(float[] values)
    {
        _values = values;
    }

    public Vector2 ToVector2()
    {
        if (_values.Length < 2)
            throw new InvalidOperationException("BeliefVector has fewer than 2 dimensions.");

        return new Vector2(_values[0], _values[1]);
    }

    public Vector3 ToVector3()
    {
        if (_values.Length < 3)
            throw new InvalidOperationException("BeliefVector has fewer than 3 dimensions.");

        return new Vector3(_values[0], _values[1], _values[2]);
    }

    public float[] ToArray()
    {
        return (float[])_values.Clone();
    }
}