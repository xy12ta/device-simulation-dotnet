﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.Azure.IoTSolutions.DeviceSimulation.Services.Clustering;
using Microsoft.Azure.IoTSolutions.DeviceSimulation.Services.Concurrency;
using Microsoft.Azure.IoTSolutions.DeviceSimulation.Services.Diagnostics;

namespace Microsoft.Azure.IoTSolutions.DeviceSimulation.PartitioningAgent
{
    public interface IPartitioningAgent
    {
        Task StartAsync();
        void Stop();
    }

    public class Agent : IPartitioningAgent
    {
        private readonly IClusterNodes clusterNodes;
        private readonly IThreadWrapper thread;
        private readonly ILogger log;
        private readonly int checkIntervalMsecs;

        private bool running;

        public Agent(
            IClusterNodes clusterNodes,
            IThreadWrapper thread,
            IClusteringConfig clusteringConfig,
            ILogger logger)
        {
            this.clusterNodes = clusterNodes;
            this.thread = thread;
            this.log = logger;
            this.checkIntervalMsecs = clusteringConfig.CheckIntervalMsecs;
            this.running = false;
        }

        public async Task StartAsync()
        {
            this.log.Info("Partitioning agent started", () => new { Node = this.clusterNodes.GetCurrentNodeId() });

            this.running = true;

            // Repeat until the agent is stopped
            while (this.running)
            {
                await this.clusterNodes.KeepAliveNodeAsync();

                // Only one process in the network becomes a master, and the master
                // is responsible for running tasks not meant to run in parallel, like
                // creating devices and partitions, and other tasks
                var isMaster = await this.clusterNodes.SelfElectToMasterNodeAsync();
                if (isMaster)
                {
                    await this.clusterNodes.RemoveStaleNodesAsync();
                }

                this.thread.Sleep(this.checkIntervalMsecs);
            }
        }

        public void Stop()
        {
            this.running = false;
        }
    }
}
