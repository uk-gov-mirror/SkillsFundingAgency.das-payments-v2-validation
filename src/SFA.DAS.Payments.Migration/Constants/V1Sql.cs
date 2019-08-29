﻿namespace SFA.DAS.Payments.Migration.Constants
{
    public static class V1Sql
    {
        public const string Commitments = @"
                WITH CommitmentsToReturn AS (
	                SELECT MAX(CAST(SUBSTRING(VersionId, 0, CHARINDEX('-', VersionId)) AS INT)) [Event ID], CommitmentId 
	                FROM [DasCommitmentsHistory]
	                WHERE EventDateTime < @inputDate
	                GROUP BY CommitmentId
                )
                SELECT * FROM DasCommitmentsHistory
                WHERE CAST(SUBSTRING(VersionId, 0, CHARINDEX('-', VersionId)) AS INT) IN (
	                SELECT [Event ID] FROM CommitmentsToReturn
                )
                ORDER BY CommitmentId
            ";

        public const string Accounts = @"
             SELECT [AccountId]
                  ,[AccountHashId]
                  ,[AccountName]
                  ,[Balance]
                  ,[VersionId]
                  ,[IsLevyPayer]
                  ,[TransferAllowance]
              FROM [DasAccounts]
            ";

        public const string Payments = @"
                SELECT R.CommitmentId [ApprenticeshipId],
                     AccountId, 
                     Uln [LearnerUln],
                     LearnRefNumber [LearnerReferenceNumber],
                     Ukprn,
                     IlrSubmissionDateTime,
                     COALESCE(PriceEpisodeIdentifier, '') [PriceEpisodeIdentifier],
                     StandardCode [LearningAimStandardCode],
                     ProgrammeType [LearningAimProgrammeType],
                     FrameworkCode [LearningAimFrameworkCode],
                     PathwayCode [LearningAimPathwayCode],
                     ApprenticeshipContractType [ContractType],
                     R.CollectionPeriodName,
                     R.TransactionType,
                     SfaContributionPercentage,
                     FundingLineType [LearningAimFundingLineType],
                     LearnAimRef [LearningAimReference],
                     CASE WHEN P.DeliveryMonth < 8 THEN P.DeliveryMonth + 5 ELSE P.DeliveryMonth - 7 END [DeliveryPeriod],
                     SUBSTRING(R.CollectionPeriodName, 1, 4) [AcademicYear],
                     FundingSource,
                     P.Amount,
                     CAST(SUBSTRING(R.CollectionPeriodName, 7, 2) AS INT) [CollectionPeriod],
                     T.SendingAccountId [TransferSendingAccountId]
	            FROM [DAS_PeriodEnd].Payments.Payments P
	            JOIN [DAS_PeriodEnd].PaymentsDue.RequiredPayments R
	                ON P.RequiredPaymentId = R.Id
                LEFT JOIN [DAS_PeriodEnd].TransferPayments.AccountTransfers T
                    ON R.Id = T.RequiredPaymentId
                WHERE R.CollectionPeriodName = @period
            ";
    }
}
