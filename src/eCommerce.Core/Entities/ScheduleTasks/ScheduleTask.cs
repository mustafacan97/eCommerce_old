﻿using eCommerce.Core.Primitives;

namespace eCommerce.Core.Entities.ScheduleTasks;

public class ScheduleTask : SoftDeletedEntity
{
    public string Name { get; set; }

    public int Seconds { get; set; }

    public string Type { get; set; }

    public DateTime? LastStartUtc { get; set; }

    public DateTime? LastEndUtc { get; set; }

    public DateTime? LastSuccessUtc { get; set; }
}
