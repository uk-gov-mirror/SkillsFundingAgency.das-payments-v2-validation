﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FastMember;
using SFA.DAS.Payments.Verification.Constants;
using SFA.DAS.Payments.Verification.DTO;

namespace SFA.DAS.Payments.Verification
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Log.Initialise();

                // Get the list of learners that we are interested in
                Console.WriteLine("Please select an learner group:");
                Console.WriteLine("");
                Console.WriteLine("\t1. All learners");
                Console.WriteLine("\t2. ACT2 Basic Day");
                Console.WriteLine("\t3. ACT2 with Refunds");
                Console.WriteLine("\t4. ACT2 with Incentives");

                var key = Console.ReadKey(true);
                Console.WriteLine();
                Log.Write($"{key.KeyChar} pressed. Processing...");

                switch (key.Key)
                {
                    case ConsoleKey.D1:
                        await InitialiseActiveLearners(Inclusions.AllLearners);
                        break;
                    case ConsoleKey.D2:
                        await InitialiseActiveLearners(Inclusions.Act2BasicDay);
                        break;
                    case ConsoleKey.D3:
                        await InitialiseActiveLearners(Inclusions.Act2Refund);
                        break;
                    case ConsoleKey.D4:
                        await InitialiseActiveLearners(Inclusions.Act2Incentives);
                        break;
                    default:
                        Console.WriteLine("Unknown response, exiting when you press any key...");
                        Console.ReadKey();
                        return;
                }

                Log.Write("Initialised learner group");

                var jobId = await Sql.InitialiseJob();

                // Get the payments
                var v1Payments = await Sql.Read<Payment>(PaymentSystem.V1, Script.Payments);
                Log.Write($"Retrieved {v1Payments.Count} V1 Payments");

                var v2Payments = await Sql.Read<Payment>(PaymentSystem.V2, Script.Payments);
                Log.Write($"Retrieved {v2Payments.Count} V2 Payments");

                var v1PaymentsWithoutV2 = v1Payments.Except(v2Payments).ToList();
                var v2PaymentsWithoutV1 = v2Payments.Except(v1Payments).ToList();
                var commonPayments = v1Payments.Intersect(v2Payments).ToList();
                Log.Write("Payment comparison complete");

                // Get the earnings
                var v1Earnings = await Sql.Read<Earning>(PaymentSystem.V1, Script.Earnings);
                Log.Write($"Retrieved {v1Earnings.Count} V1 Earnings");

                var v2Earnings = await Sql.Read<Earning>(PaymentSystem.V2, Script.Earnings);
                Log.Write($"Retrieved {v2Earnings.Count} V2 Earnings");

                var v1EarningsWithoutV2 = v1Earnings.Except(v2Earnings).ToList();
                var v2EarningsWithoutV1 = v2Earnings.Except(v1Earnings).ToList();
                var commonEarnings = v1Earnings.Intersect(v2Earnings).ToList();
                Log.Write("Earning comparison complete");

                // Get the required payments
                var v1RequiredPayments = await Sql.Read<RequiredPayment>(PaymentSystem.V1, Script.RequiredPayments);
                Log.Write($"Retrieved {v1RequiredPayments.Count} V1 Required Payments");

                var v2RequiredPayments = await Sql.Read<RequiredPayment>(PaymentSystem.V2, Script.RequiredPayments);
                Log.Write($"Retrieved {v2RequiredPayments.Count} V2 Required Payments");

                var v1RequiredPaymentsWithoutV2 = v1RequiredPayments.Except(v2RequiredPayments).ToList();
                var v2RequiredPaymentsWithoutV1 = v2RequiredPayments.Except(v1RequiredPayments).ToList();
                var commonRequiredPayments = v1RequiredPayments.Intersect(v2RequiredPayments).ToList();
                Log.Write("Required Payments comparison complete");

                // For V1 payments without V2 - are the earnings the same?



                // High level summary
                var v1PaymentsByTransactionType = v1Payments.ToLookup(x => x.TransactionType);
                var v2PaymentsByTransactionType = v2Payments.ToLookup(x => x.TransactionType);
                var v1RequiredPaymentsByTransactionType = v1RequiredPayments.ToLookup(x => x.TransactionType);
                var v2RequiredPaymentsByTransactionType = v2RequiredPayments.ToLookup(x => x.TransactionType);
                var summary = new List<HighLevelSummary>();

                // For each transaction type
                for (int i = 1; i < 17; i++)
                {
                    // Create a new row
                    summary.Add(new HighLevelSummary
                    {
                        TransactionType = i,
                        // Aggregate all amounts for this transaction type
                        V1PaymentsAmount = v1PaymentsByTransactionType[i].Sum(x => x.Amount),
                        V2PaymentsAmount = v2PaymentsByTransactionType[i].Sum(x => x.Amount),
                        V1RequiredPaymentsAmount = v1RequiredPaymentsByTransactionType[i].Sum(x => x.Amount),
                        V2RequiredPaymentsAmount = v2RequiredPaymentsByTransactionType[i].Sum(x => x.Amount),
                        V1EarningsAmount = CalculateEarnings(v1Earnings, i),
                        V2EarningsAmount = CalculateEarnings(v2Earnings, i),
                    });
                }

                using (var dataStream = Excel.CreateExcelDocumentWithSheets(
                    (summary, "High Level Summary"),
                    (v1PaymentsWithoutV2, "V1 Payments without V2"),
                    (v2PaymentsWithoutV1, "V2 Payments without V1"),
                    //(commonPayments, "Common Payments"),
                    (v1EarningsWithoutV2, "V1 Earnings without V2"),
                    (v2EarningsWithoutV1, "V2 Earnings without V1"),
                    //(commonEarnings, "Common Earnings"),
                    (v1RequiredPaymentsWithoutV2, "V1 Required Payments without V2"),
                    (v2RequiredPaymentsWithoutV1, "V2 Required Payments without V1")
                    //(commonRequiredPayments, "Common Required Payments")
                    ))
                using (var file = File.Create("V2 Verification Results.xlsx"))
                {
                    dataStream.CopyTo(file);
                }

                Log.Write("Finished creating Excel file");

                Log.Write($"Memory usage: {GC.GetTotalMemory(false):###,###,###,###,###,### bytes}");

                Log.Write("Saving to SQL");

                // Save 3 tables with a flag for where the record came from
                SetVerificationResult(v1PaymentsWithoutV2, VerificationResult.V1Only, jobId);
                await Sql.Write(PaymentSystem.Output, v1PaymentsWithoutV2, "Payments");
                SetVerificationResult(v2PaymentsWithoutV1, VerificationResult.V2Only, jobId);
                await Sql.Write(PaymentSystem.Output, v2PaymentsWithoutV1, "Payments");
                SetVerificationResult(commonPayments, VerificationResult.Okay, jobId);
                await Sql.Write(PaymentSystem.Output, commonPayments, "Payments");

                SetVerificationResult(v1EarningsWithoutV2, VerificationResult.V1Only, jobId);
                await Sql.Write(PaymentSystem.Output, v1EarningsWithoutV2, "Earnings");
                SetVerificationResult(v2EarningsWithoutV1, VerificationResult.V2Only, jobId);
                await Sql.Write(PaymentSystem.Output, v2EarningsWithoutV1, "Earnings");
                SetVerificationResult(commonEarnings, VerificationResult.Okay, jobId);
                await Sql.Write(PaymentSystem.Output, commonEarnings, "Earnings");

                SetVerificationResult(v1RequiredPaymentsWithoutV2, VerificationResult.V1Only, jobId);
                await Sql.Write(PaymentSystem.Output, v1RequiredPaymentsWithoutV2, "RequiredPayments");
                SetVerificationResult(v2RequiredPaymentsWithoutV1, VerificationResult.V2Only, jobId);
                await Sql.Write(PaymentSystem.Output, v2RequiredPaymentsWithoutV1, "RequiredPayments");
                SetVerificationResult(commonRequiredPayments, VerificationResult.Okay, jobId);
                await Sql.Write(PaymentSystem.Output, commonRequiredPayments, "RequiredPayments");


                Log.Write($"Job complete with JobId: {jobId}, press any key to close");
                Console.ReadKey();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
        }

        private static readonly TypeAccessor EarningsAccesor = TypeAccessor.Create(typeof(Earning));
        private static decimal CalculateEarnings(List<Earning> earnings, int transactionType)
        {
            var total = 0m;
            foreach (var earning in earnings)
            {
                total += (decimal)EarningsAccesor[earning, $"TransactionType{transactionType:D2}"];
            }

            return total;
        }

        private static Task InitialiseActiveLearners(Inclusions inclusions)
        {
            return Sql.InitialiseLearnerTables(inclusions);
        }

        private static void SetVerificationResult<T>(List<T> input, VerificationResult result, int jobId) where T : IContainVerificationResults
        {
            for (int i = 0; i < input.Count; i++)
            {
                var temp = input[i];
                temp.VerificationResult = result;
                temp.JobId = jobId;
                input[i] = temp;
            }
        }
    }
}
