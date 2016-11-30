﻿using GAF;
using GAF.Extensions;
using GAF.Operators;
using QuantConnect.Api;
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.HistoricalData;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Logging;
using QuantConnect.Messaging;
using QuantConnect.Packets;
using QuantConnect.Queues;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Threading;

namespace Optimization
{

    public class Program
    {

        #region Declarations
        private static readonly Random random = new Random();
        private static AppDomainSetup _ads;
        private static string _callingDomainName;
        private static string _exeAssembly;
        static StreamWriter writer;
        public static Dictionary<string, decimal> Results;
        static OptimizerConfiguration config;
        #endregion

        public static void Main(string[] args)
        {
            Results = new Dictionary<string, decimal>();
            string path = System.Configuration.ConfigurationManager.AppSettings["ConfigPath"];
            System.IO.File.Copy(path, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"), true);
            //todo: map from config
            config = new OptimizerConfiguration();

            _ads = SetupAppDomain();
            writer = System.IO.File.AppendText("optimizer.txt");


            //create the population
            var population = new Population();
            population.EvaluateInParallel = true;

            //create the chromosomes
            for (var p = 0; p < config.PopulationSize; p++)
            {
                var chromosome = GeneFactory.Spawn();
                population.Solutions.Add(chromosome);
            }

            //create the GA itself 
            var ga = new GeneticAlgorithm(population, CalculateFitness);
            //subscribe to the GAs Generation Complete event 
            ga.OnGenerationComplete += ga_OnGenerationComplete;
            ga.OnRunComplete += ga_OnRunComplete;

            //create the genetic operators 
            if (config.EliteEnabled)
            {
                var elite = new Elite(config.ElitePercent);
                ga.Operators.Add(elite);
            }
            if (config.RandomReplaceEnabled)
            {
                var bottom = new ReplaceBottomOperator(config.RandomReplacePercent);
                ga.Operators.Add(bottom);
            }
            if (config.CrossoverEnabled)
            {
                var crossover = new Crossover(config.CrossoverPercent, true, CrossoverType.DoublePoint, ReplacementMethod.GenerationalReplacement);
                ga.Operators.Add(crossover);
            }
            //run the GA 
            ga.Run(Terminate);

            writer.Close();

            Console.ReadKey();
        }

        static AppDomainSetup SetupAppDomain()
        {
            _callingDomainName = Thread.GetDomain().FriendlyName;

            // Get and display the full name of the EXE assembly.
            _exeAssembly = Assembly.GetEntryAssembly().FullName;

            // Construct and initialize settings for a second AppDomain.
            AppDomainSetup ads = new AppDomainSetup();
            ads.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;

            ads.DisallowBindingRedirects = false;
            ads.DisallowCodeDownload = true;
            ads.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

            return ads;
        }

        static Runner CreateRunClassInAppDomain(ref AppDomain ad)
        {
            // Create the second AppDomain.
            var name = Guid.NewGuid().ToString("x");
            ad = AppDomain.CreateDomain(name, null, _ads);

            // Create an instance of MarshalbyRefType in the second AppDomain. 
            // A proxy to the object is returned.
            Runner rc = (Runner)ad.CreateInstanceAndUnwrap(_exeAssembly, typeof(Runner).FullName);

            ad.SetData("Results", Results);

            return rc;
        }

        static void ga_OnRunComplete(object sender, GaEventArgs e)
        {
            var fittest = e.Population.GetTop(1)[0];
            foreach (var gene in fittest.Genes)
            {
                var pair = (KeyValuePair<string, object>)gene.ObjectValue;
                Output("{0}: value {1}", pair.Key, pair.Value);
            }
        }

        private static void ga_OnGenerationComplete(object sender, GaEventArgs e)
        {
            var fittest = e.Population.GetTop(1)[0];
            Output("Generation: {0}, Fitness: {1}, Sharpe: {2}", e.Generation, fittest.Fitness, (fittest.Fitness * 200) - 10);
        }

        public static double CalculateFitness(Chromosome chromosome)
        {
            try
            {
                var sharpe = RunAlgorithm(chromosome);
                return (System.Math.Max(sharpe, -10) + 10) / 200;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static double RunAlgorithm(Chromosome chromosome)
        {
            AppDomain ad = null;
            Runner rc = CreateRunClassInAppDomain(ref ad);
            string output = "";

            foreach (var item in chromosome.Genes)
            {
                var pair = (KeyValuePair<string, object>)item.ObjectValue;
                output += " " + pair.Key + " " + pair.Value.ToString();
            }

            var sharpe = (double)rc.Run(chromosome.Genes);

            Results = (Dictionary<string, decimal>)ad.GetData("Results");

            AppDomain.Unload(ad);
            output += string.Format(" Sharpe:{0}", sharpe);

            Output(output);

            return sharpe;
        }

        public static bool Terminate(Population population, int currentGeneration, long currentEvaluation)
        {
            bool canTerminate = currentGeneration > config.Generations;
            return canTerminate;
        }

        public static void Output(string line, params object[] format)
        {
            Output(string.Format(line, format));
        }

        public static void Output(string line)
        {
            writer.Write(DateTime.Now.ToString("u"));
            writer.Write(line);
            writer.Write(writer.NewLine);
            writer.Flush();
            Console.WriteLine(line);
        }

    }
}
