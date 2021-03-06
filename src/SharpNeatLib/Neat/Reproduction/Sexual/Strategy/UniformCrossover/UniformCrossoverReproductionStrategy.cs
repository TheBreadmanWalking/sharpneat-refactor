﻿using Redzen;
using Redzen.Numerics;
using Redzen.Random;
using Redzen.Structures;
using SharpNeat.Neat.Genome;
using static SharpNeat.Neat.Reproduction.Sexual.Strategy.UniformCrossover.UniformCrossoverReproductionStrategyUtils;

namespace SharpNeat.Neat.Reproduction.Sexual.Strategy.UniformCrossover
{
    /// <summary>
    /// Uniform crossover.
    /// 
    /// The genes of the two parent genomes are aligned by innovation ID. The new child genome
    /// takes genes from each of the parents with a given probability (e.g. 50%).
    /// </summary>
    public class UniformCrossoverReproductionStrategy<T> : ISexualReproductionStrategy<T>
        where T : struct
    {
        readonly MetaNeatGenome<T> _metaNeatGenome;
        readonly INeatGenomeBuilder<T> _genomeBuilder;
        readonly Int32Sequence _genomeIdSeq;
        readonly Int32Sequence _generationSeq;
        readonly IRandomSource _rng;
        readonly ConnectionGeneListBuilder<T> _builder;

        #region Constructor

        public UniformCrossoverReproductionStrategy(
            MetaNeatGenome<T> metaNeatGenome,
            INeatGenomeBuilder<T> genomeBuilder,
            Int32Sequence genomeIdSeq,
            Int32Sequence generationSeq,
            IRandomSource rng)
        {
            _metaNeatGenome = metaNeatGenome;
            _genomeBuilder = genomeBuilder;
            _genomeIdSeq = genomeIdSeq;
            _generationSeq = generationSeq;

            _rng = rng;
            _builder = new ConnectionGeneListBuilder<T>(_metaNeatGenome.IsAcyclic, 1024);
        }

        #endregion

        #region Public Methods

        public NeatGenome<T> CreateGenome(NeatGenome<T> parent1, NeatGenome<T> parent2)
        {
            try
            {
                return CreateGenomeInner(parent1, parent2);
            }
            finally
            {
                // Clear down ready for re-use of the builder on the next call to CreateGenome().
                // Re-using in this way avoids having to de-alloc and re-alloc memory, thus reducing garbage collection overhead.
                _builder.Clear();
            }
        }

        #endregion

        #region Private Methods

        private NeatGenome<T> CreateGenomeInner(NeatGenome<T> parent1, NeatGenome<T> parent2)
        {
            // Randomly select one parent as being the primary parent.
            if(_rng.NextBool()) {
                VariableUtils.Swap(ref parent1, ref parent2);
            }

            // Enumerate over the connection genes in both parents.
            foreach(var geneIndexPair in EnumerateParentGenes(parent1.ConnectionGenes, parent2.ConnectionGenes))
            {
                // Create a connection gene based on the current position in both parents.
                ConnectionGene<T>? connGene = CreateConnectionGene(
                    parent1.ConnectionGenes, parent2.ConnectionGenes,
                    geneIndexPair.Item1, geneIndexPair.Item2,
                    out bool isSecondaryGene);

                if(connGene.HasValue)
                {
                    // Attempt to add the gene to the child genome we are building.
                    _builder.TryAddGene(connGene.Value, isSecondaryGene);
                }
            }

            // Convert the genes to the structure required by NeatGenome.
            var connGenes = _builder.ToConnectionGenes();

            // Create and return a new genome.
            return _genomeBuilder.Create(
                _genomeIdSeq.Next(), 
                _generationSeq.Peek,
                connGenes);
        }

        #endregion

        #region Private Methods [Low Level]

        private ConnectionGene<T>? CreateConnectionGene(
            ConnectionGenes<T> connGenes1,
            ConnectionGenes<T> connGenes2,
            int idx1, int idx2,
            out bool isSecondaryGene)
        {
            // Select gene at random if it is present on both parents.
            if(-1 != idx1 && -1 != idx2)
            {
                if(_rng.NextBool())
                {
                    isSecondaryGene = false;
                    return CreateConnectionGene(connGenes1, idx1);
                }
                else
                {
                    isSecondaryGene = true;
                    return CreateConnectionGene(connGenes2, idx2);
                }
            }

            // Use the primary parent's gene if it has one.
            if(-1 != idx1) 
            {
                isSecondaryGene = false;
                return CreateConnectionGene(connGenes1, idx1);
            }

            // Otherwise use the secondary parent's gene stochastically.
            if(DiscreteDistributionUtils.SampleBinaryDistribution(_metaNeatGenome.SecondaryParentGeneProbability, _rng))
            {
                isSecondaryGene = true;
                return CreateConnectionGene(connGenes2, idx2);
            }
            
            isSecondaryGene = false;
            return null;
        }

        private ConnectionGene<T> CreateConnectionGene(ConnectionGenes<T> connGenes, int idx)
        {
            return new ConnectionGene<T>(
                connGenes._connArr[idx],
                connGenes._weightArr[idx]);
        }

        #endregion
    }
}
