﻿CREATE TABLE [Verification].[RequiredPayments]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY,
	[CommitmentId] BIGINT NULL, 
    [AccountId] BIGINT NULL, 
    [LearnerReferenceNumber] NVARCHAR(50) NOT NULL, 
    [Ukprn] BIGINT NOT NULL, 
    [PriceEpisodeIdentifier] NVARCHAR(50) NOT NULL, 
    [LearningAimStandardCode] INT NULL, 
    [LearningAimProgrammeType] INT NULL, 
    [LearningAimFrameworkCode] INT NULL, 
    [LearningAimPathwayCode] INT NULL, 
    [ContractType] INT NOT NULL, 
    [CollectionPeriod] int NOT NULL, 
    [TransactionType] INT NOT NULL, 
    [SfaContributionPercentage] DECIMAL(18, 5) NOT NULL, 
    [LearningAimFundingLineType] NVARCHAR(150) NOT NULL, 
    [LearningAimReference] NVARCHAR(50) NOT NULL, 
    [DeliveryPeriod] INT NOT NULL,
	AcademicYear int NOT NULL,
	Amount money NOT NULL, 
    [LearnerUln] BIGINT NOT NULL, 
    [VerificationResult] INT NOT NULL
)
