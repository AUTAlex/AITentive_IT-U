using System;
using System.Collections.Generic;

public interface IBelievableObject
{
    bool IsVisible { get; set; }

    void UpdateBeliefState(float updateTime);

    event Action<IBelievableObject> Destructed;

    void InitBeliefState(System.Random rand, BelievableObjectConfig believableObjectConfig);
}