﻿using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Features.PoA.Voting;

namespace Stratis.Bitcoin.Features.PoA
{
    public interface IFederationHistory
    {
        /// <summary>Gets the federation member for a specified block.</summary>
        /// <param name="chainedHeader">Identifies the block and timestamp.</param>
        /// <returns>The federation member or <c>null</c> if the member could not be determined.</returns>
        /// <exception cref="ConsensusErrorException">In case timestamp is invalid.</exception>
        IFederationMember GetFederationMemberForBlock(ChainedHeader chainedHeader);

        /// <summary>Gets the federation member for a specified block.</summary>
        /// <param name="chainedHeader">Identifies the block and timestamp.</param>
        /// <param name="federation">The federation members at the block height.</param>
        /// <returns>The federation member or <c>null</c> if the member could not be determined.</returns>
        IFederationMember GetFederationMemberForBlock(ChainedHeader chainedHeader, List<IFederationMember> federation);

        /// <summary>Gets the federation for a specified block.</summary>
        /// <param name="chainedHeader">Identifies the block and timestamp.</param>
        /// <returns>The federation member or <c>null</c> if the member could not be determined.</returns>
        List<IFederationMember> GetFederationForBlock(ChainedHeader chainedHeader);
    }

    public class FederationHistory : IFederationHistory
    {
        private readonly IFederationManager federationManager;
        private readonly VotingManager votingManager;
        private readonly Dictionary<uint256, IFederationMember> minersByBlockHash;

        public FederationHistory(IFederationManager federationManager, VotingManager votingManager = null)
        {
            this.federationManager = federationManager;
            this.votingManager = votingManager;
            this.minersByBlockHash = new Dictionary<uint256, IFederationMember>();
        }

        /// <inheritdoc />
        public List<IFederationMember> GetFederationForBlock(ChainedHeader chainedHeader)
        {
            return (this.votingManager == null) ? this.federationManager.GetFederationMembers() : this.votingManager.GetModifiedFederation(chainedHeader);
        }

        /// <inheritdoc />
        public IFederationMember GetFederationMemberForBlock(ChainedHeader chainedHeader)
        {
            if (this.minersByBlockHash.TryGetValue(chainedHeader.HashBlock, out IFederationMember federationMember))
                return federationMember;

            return GetFederationMemberForBlock(chainedHeader, this.GetFederationForBlock(chainedHeader));
        }

        /// <inheritdoc />
        public IFederationMember GetFederationMemberForBlock(ChainedHeader chainedHeader, List<IFederationMember> federation)
        {
            if (this.minersByBlockHash.TryGetValue(chainedHeader.HashBlock, out IFederationMember federationMember))
                return federationMember;

            // Try to provide the public key that signed the block.
            try
            {
                var header = chainedHeader.Header as PoABlockHeader;

                var signature = ECDSASignature.FromDER(header.BlockSignature.Signature);
                for (int recId = 0; recId < 4; recId++)
                {
                    PubKey pubKeyForSig = PubKey.RecoverFromSignature(recId, signature, header.GetHash(), true);
                    if (pubKeyForSig == null)
                        break;

                    federationMember = federation.FirstOrDefault(m => m.PubKey == pubKeyForSig);
                    if (federationMember != null)
                    {
                        this.minersByBlockHash[chainedHeader.HashBlock] = federationMember;
                        return federationMember;
                    }
                }
            }
            catch (Exception)
            {
            }

            return null;
        }
    }
}