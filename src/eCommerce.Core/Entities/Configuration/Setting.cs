﻿using eCommerce.Core.Entities.Localization;
using eCommerce.Core.Primitives;

namespace eCommerce.Core.Entities.Configuration;

public class Setting : BaseEntity, ILocalizedEntity
{
    #region Constructure and Destructure

    public Setting()
    {
    }

    public Setting(string name, string value)
    {
        Name = name;
        Value = value;
    }

    #endregion

    #region Public Properties

    public string Name { get; set; }

    public string? Value { get; set; }

    #endregion
}
