﻿using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Client.Models
{
    public class ClientInfo : IClient
    {
        public string? Name { get; set; }

        public string? Gender { get; set; }

        public int Age { get; set; } = 25;

        ////public decimal? PAL { get; set; }

        public decimal? Height { get; set; }

        public decimal? Weight { get; set; }

        public Guid ClientId { get; set; } = new Guid();

        public string SpecialPhysiologicalPeriod { get; set; } = string.Empty;

    }

    public class Archive(IClient client)
    {
        public IClient Client => client;

        public EnergyCalculator? CurrentEnergyCalculator { get; set; }

        public DRIs? DRIs { get; set; }
    }
}
