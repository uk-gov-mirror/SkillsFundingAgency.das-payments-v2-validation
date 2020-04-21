﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SDA.DAS.Payments.ConsoleUtilities;
using SFA.DAS.Payments.AnonymiserTool.DatabaseEntities;
using SFA.DAS.Payments.AnonymiserTool.Dto;

namespace SFA.DAS.Payments.AnonymiserTool
{
    static class DataManipulation
    {
        public static async Task<List<Apprenticeship>> AlterUlns(ApprenticeshipData apprenticeshipData, Dictionary<long, ReadOptimisedProviderData> anonymisedProviders)
        {
            var apprenticeshipsToRemove = new List<Apprenticeship>();
            foreach (var apprenticeship in apprenticeshipData.Apprenticeships)
            {
                var ukprn = apprenticeship.Ukprn;
                if (!anonymisedProviders.ContainsKey(ukprn))
                {
                    apprenticeshipsToRemove.Add(apprenticeship);
                    continue;
                }

                var providerData = anonymisedProviders[ukprn];
                if (!providerData.OptimisedLearners.ContainsKey(apprenticeship.Uln))
                {
                    apprenticeshipsToRemove.Add(apprenticeship);
                    continue;
                }

                var listOfChangedLearners = providerData.OptimisedLearners[apprenticeship.Uln];
                foreach (var changedLearner in listOfChangedLearners)
                {
                    if (changedLearner.OldUln != apprenticeship.Uln)
                    {
                        await Logger.Log(
                            $"Multiple learners for UKPRN: {ukprn} and ULN: {apprenticeship.Uln} - results are not guaranteed");
                        foreach (var learner in listOfChangedLearners)
                        {
                            await Logger.Log($"New ULN: {learner.NewUln}", 1);
                        }
                    }

                    apprenticeship.Uln = changedLearner.NewUln;
                }
            }

            return apprenticeshipsToRemove;
        }

        public static async Task RemoveApprenticeships(ApprenticeshipData apprenticeshipData,
            List<Apprenticeship> apprenticeshipsToRemove)
        {
            await Logger.Log($"Removing {apprenticeshipsToRemove.Count} apprenticeships");
            await Logger.Log($"Optimising the data...", 1);

            var pausesByApprenticeshipId = apprenticeshipData
                .ApprenticeshipPauses
                .ToLookup(x => x.ApprenticeshipId);

            var priceEpisodesByApprenticeshipId = apprenticeshipData
                .ApprenticeshipPriceEpisodes
                .ToLookup(x => x.ApprenticeshipId);

            var counter = 0;

            foreach (var apprenticeship in apprenticeshipsToRemove)
            {
                if (pausesByApprenticeshipId.Contains(apprenticeship.Id))
                {
                    foreach (var apprenticeshipPause in pausesByApprenticeshipId[apprenticeship.Id])
                    {
                        apprenticeshipData.ApprenticeshipPauses.Remove(apprenticeshipPause);
                    }
                }

                if (priceEpisodesByApprenticeshipId.Contains(apprenticeship.Id))
                {
                    foreach (var apprenticeshipPriceEpisode in priceEpisodesByApprenticeshipId[apprenticeship.Id])
                    {
                        apprenticeshipData.ApprenticeshipPriceEpisodes.Remove(apprenticeshipPriceEpisode);
                    }
                }
                
                apprenticeshipData.Apprenticeships.Remove(apprenticeship);

                counter++;
                if (counter % 1000 == 0)
                {
                    await Logger.Log($"Removed {counter} apprenticeships", 1);
                }
            }
        }
    }
}
