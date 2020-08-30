// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#pragma once

#include <cassert>
#include <complex>
#include <ctime>
#include <fstream>
#include <iostream>
#include <iterator>
#include <limits>
#include <random>
#include <vector>
#include <unordered_set>
#include <unordered_map>
#include <string.h>

#include "types.hpp"
#include "gates.hpp"

#include "external/fused.hpp"

namespace Microsoft
{
    namespace Quantum
    {
        namespace SIMULATOR
        {
            namespace detail
            {
                inline std::size_t get_register(const std::vector<unsigned>& qs, std::size_t basis_state)
                {
                    std::size_t result = 0;
                    for (unsigned i = 0; i < qs.size(); ++i)
                        result |= ((basis_state >> qs[i]) & 1) << i;
                    return result;

                }

                inline std::size_t set_register(const std::vector<unsigned>& qs, std::size_t qmask, std::size_t basis_state, std::size_t original = 0ull)
                {
                    std::size_t result = original & ~qmask;
                    for (unsigned i = 0; i < qs.size(); ++i)
                        result |= ((basis_state >> i) & 1) << qs[i];
                    return result;
                }
            }

            // Creating a gate wrapper datatype to represent a gate in a cluster
            class GateWrapper {
            public:
                GateWrapper(std::vector<unsigned> controls, unsigned target, TinyMatrix<ComplexType, 2> mat) : controls_(controls), target_(target), mat_(mat) {}
                std::vector<unsigned> get_controls() { return controls_; }
                unsigned get_target() { return target_; }
                TinyMatrix<ComplexType, 2> get_mat() { return mat_; }
            private:
                std::vector<unsigned> controls_;
                unsigned target_;
                TinyMatrix<ComplexType, 2> mat_;
            };

            // Creating a cluster datatype for new scheduling logic
            class Cluster {
            public:
                Cluster(std::vector<unsigned> qids, std::vector<GateWrapper> gates) : qids_(qids), gates_(gates) {}
                std::vector<unsigned> get_qids() { return qids_; }
                std::vector<GateWrapper> get_gates() { return gates_; }

                void setQids(std::vector<unsigned> qids) {
                    qids_ = qids;
                }

                void append_gates(std::vector<GateWrapper> gates) {
                    gates_.insert(gates_.end(), gates.begin(), gates.end());
                }

                size_t size() {
                    return gates_.size();
                }

                // Greedy method that finds next appropriate cluster
                std::pair<Cluster, std::vector<unsigned>> next_cluster(std::vector<Cluster>& nextClusters, unsigned maxWidth) {
                    std::vector<unsigned>   myUnion;                                // My qubits touched + Next qubits touched
                    std::vector<unsigned>   myDiff;                                 // New qubits touched by Next
                    std::vector<unsigned>   myInter;                                // Old qubits touched by Next
                    std::vector<unsigned>   allInter;                               // My qubits + All touched qubits
                    std::set<unsigned>      myTouched(qids_.begin(), qids_.end());  // My qubits touched
                    std::set<unsigned>      allTouched = myTouched;                 // All the qubits touched so far

                    int lastNexts = (int)nextClusters.size() - 1;                   // nexts are in reverse order (from above)
                    for (int i = 0; i <= lastNexts; i++) {                          // Look at the clusters that follow us
                        auto   nextQs = nextClusters[lastNexts-i].get_qids();       // Pull off one future cluster
                        std::sort(nextQs.begin(), nextQs.end());                    // Has to be sorted for set operations
                        myUnion.clear();
                        std::set_union(nextQs.begin(), nextQs.end(),                // See what qubits we and the future cluster touch
                            myTouched.begin(), myTouched.end(),
                            std::back_inserter(myUnion));
                        if (myUnion.size() <= maxWidth) {                           // It's a candiate if it's not beyond our allowed width
                            myDiff.clear();
                            std::set_difference(nextQs.begin(), nextQs.end(),       // Figure out if any of the future qubits aren't already seen by us
                                myTouched.begin(), myTouched.end(),
                                std::back_inserter(myDiff));
                            allInter.clear();
                            std::set_intersection(myDiff.begin(), myDiff.end(),     // These are any new qubits that might have already been touched
                                allTouched.begin(), allTouched.end(),
                                std::back_inserter(allInter));
                            if (allInter.size() == 0) {                             // If the new qubits are untouched... then this is allowed
                                auto cl = nextClusters[lastNexts-i];
                                nextClusters.erase(nextClusters.begin() + (lastNexts-i));       // Remove the future cluster
                                return std::make_pair(cl, myUnion);                 // ... and add it to our cluster (done above)
                            }
                        }
                        myInter.clear();
                        std::set_intersection(nextQs.begin(), nextQs.end(),         // If a future cluster touches any of our qubits... we've hit a hard wall
                            myTouched.begin(), myTouched.end(),
                            std::back_inserter(myInter));
                        if (myInter.size() != 0) break;

                        allTouched.insert(nextQs.begin(), nextQs.end());            // Add in all qubits touched, and try the next cluster
                    }
                    Cluster defCl = Cluster({}, {});                                // Couldn't find any more clusters to add
                    std::vector<unsigned> defVec = {};
                    return std::make_pair(defCl, defVec);
                }
        private:
            std::vector<unsigned> qids_;
            std::vector<GateWrapper> gates_;
            };

/// A wave function class to store and manipulate the state of qubits

template <class T = ComplexType>
class Wavefunction
{
public:
    using value_type = T;
    using qubit_t = unsigned;
    using RngEngine = std::mt19937;

    constexpr qubit_t invalid_qubit() const { return std::numeric_limits<qubit_t>::max(); }

    /// allocate a wave function for zero qubits
    Wavefunction(unsigned /*ignore*/) : num_qubits_(0), wfn_(1, 1.), usage_(0)
    {
        rng_.seed(std::clock());
    }

    void reset()
    {
        fused_.reset();
        rng_.seed(std::clock());
        num_qubits_ = 0;
        wfn_.resize(1);
        wfn_[0] = 1.;
        qubitmap_.resize(0);
    }

    ~Wavefunction()
    {
        flush();
    }

    unsigned qubit(unsigned q) const
    {
        assert(qubitmap_[q] != invalid_qubit());
        return qubitmap_[q];
    }

    unsigned qubit(Gates::OneQubitGate const& g) const
    {
        return qubit(g.qubit());
    }

    void flush() const
    {
        int maxSpan = fused_.maxSpan();
        auto clusters = make_clusters(maxSpan, gatelist_); //making clusters with gates in the queue

        if (clusters.size() == 0) {
            fused_.flush(wfn_);
        }
        else {
            // logic to flush gates in each cluster
            for (int i = 0; i < clusters.size(); i++) {
                Cluster cl = clusters.at(i);

                for (GateWrapper gate : cl.get_gates()) {
                    std::vector<unsigned> cs = gate.get_controls();
                    if (cs.size() == 0) {
                        fused_.apply(wfn_, gate.get_mat(), qubit(gate.get_target()));
                    }
                    else {
                        fused_.apply_controlled(wfn_, gate.get_mat(), qubits(cs), qubit(gate.get_target()));
                    }
                }

                fused_.flush(wfn_);
            }
        }
        gatelist_.clear();
    }

    /// allocate a qubit and grow the wave function
    unsigned allocate()
    {
        assert(usage_ != 2);
        usage_ = 1;
        flush();
        wfn_.resize(2 * wfn_.size());
        auto it = std::find(qubitmap_.begin(), qubitmap_.end(), invalid_qubit());
        if (it != qubitmap_.end())
        {
            unsigned num = static_cast<unsigned>(it - qubitmap_.begin());
            qubitmap_[num] = num_qubits_++;
            return num;
        }
        else {
            qubitmap_.push_back(num_qubits_++);
            return static_cast<unsigned>(qubitmap_.size() - 1);
        }
    }

    /// allocate a qubit and grow the wave function
    void allocateQubit(unsigned id)
    {
        assert(usage_ != 1);
        usage_ = 2;
        flush();
        wfn_.resize(2 * wfn_.size());
        if (id < qubitmap_.size()) {
            qubitmap_[id] = num_qubits_++;
        }
        else {
            assert(id == qubitmap_.size());
            qubitmap_.push_back(num_qubits_++);
        }
        assert((wfn_.size() >> num_qubits_) == 1);
    }

    /// release the specified qubit
    /// \pre the qubit has to be in a classical state in the computational basis
    void release(qubit_t q)
    {
        unsigned p = qubit(q); //returns qubitmap_[q]
        flush();
        kernels::collapse(wfn_, p, getvalue(q), true);
        for (int i = 0; i < qubitmap_.size(); ++i)
            if (qubitmap_[i] > p && qubitmap_[i] != invalid_qubit())
                qubitmap_[i]--;
        qubitmap_[q] = invalid_qubit();
        --num_qubits_;
    }

    /// the number of used qubits
    qubit_t num_qubits() const
    {
        return num_qubits_;
    }

    /// probability of measuring a 1
    double probability(qubit_t q) const
    {
        flush();
        return kernels::probability(wfn_, qubit(q));
    }

    /// probability of jointly measuring a 1
    double jointprobability(std::vector<qubit_t> const& qs) const
    {
        flush();
        std::vector<qubit_t> ps = qubits(qs);
        return kernels::jointprobability(wfn_, ps);
    }

    /// probability of jointly measuring a 1
    double jointprobability(std::vector<Gates::Basis> const& bs, std::vector<qubit_t> const& qs) const
    {
        flush();
        std::vector<qubit_t> ps = qubits(qs);
        return kernels::jointprobability(wfn_, bs, ps);
    }

    /// measure a qubit
    bool measure(qubit_t q)
    {
        flush();
        std::uniform_real_distribution<double> uniform(0., 1.);
        bool result = (uniform(rng_) < probability(q));
        kernels::collapse(wfn_, qubit(q), result);
        kernels::normalize(wfn_);
        return result;
    }

    bool jointmeasure(std::vector<qubit_t> const& qs)
    {
        flush();
        std::vector<qubit_t> ps = qubits(qs);
        std::uniform_real_distribution<double> uniform(0., 1.);
        bool result = (uniform(rng_) < jointprobability(qs));
        kernels::jointcollapse(wfn_, ps, result);
        kernels::normalize(wfn_);
        return result;
    }

    void apply_controlled_exp(std::vector<Gates::Basis> const& bs,
        double phi,
        std::vector<unsigned> const& cs,
        std::vector<unsigned> const& qs)
    {
        flush();
        kernels::apply_controlled_exp(wfn_, bs, phi, qubits(cs), qubits(qs));
    }

    /// checks if the qubit is in classical state
    bool isclassical(qubit_t q) const
    {
        flush();
        return kernels::isclassical(wfn_, qubit(q));
    }

    /// returns the classical value of a qubit (if classical)
    /// \pre the qubit has to be in a classical state in the computational basis
    bool getvalue(qubit_t q) const
    {
        flush();
        assert(isclassical(q));
        int res = kernels::getvalue(wfn_, qubit(q));
        if (res == 2)
            std::cout << *this;

        assert(res < 2);
        return res == 1;
    }

    /// the stored wave function as a vector
    WavefunctionStorage const& data() const
    {
        flush();
        return wfn_;
    }

    /// seed the random number engine for measurements
    void seed(unsigned s)
    {
        rng_.seed(s);
    }

    //method that makes clusters to be flushed
    std::vector<Cluster> make_clusters(unsigned fuseSpan, std::vector<GateWrapper> gates) const {
        std::vector<Cluster> curClusters;

        if (gates.size() > 0) {
            //creating initial cluster containing one gate each
            for (int i = 0; i < gates.size(); i++) {
                std::vector<unsigned> qids;
                std::vector<unsigned> controlQids = gates[i].get_controls();
                if (controlQids.size() > 0) {
                    qids = controlQids;
                }
                qids.push_back(gates[i].get_target());
                Cluster newCl = Cluster(qids, { gates[i] });
                curClusters.push_back(newCl);
            }
            //creating clusters using greedy algorithm
            for (int i = 1; i < (int)fuseSpan + 1; i++) {                                   // Build clusters of width 1,2,...
                std::reverse(curClusters.begin(), curClusters.end());                       // Keep everything in reverse order
                auto prevClusters = curClusters;                                            // Save away the last set of clusters built
                curClusters.clear();
                auto prevCluster = prevClusters.back();                                     // Pop the first cluster
                prevClusters.pop_back();
                while (prevClusters.size() > 0)  {                                          // While there are more clusters...
                    auto foundCompat = prevCluster.next_cluster(prevClusters, i);           // See if we can accumlate anyone who follows
                    Cluster clusterFound = foundCompat.first;
                    std::vector<unsigned> foundTotQids = foundCompat.second;
                    if (clusterFound.get_gates().size() == 0 ||                             // Can't append any more clusters to this one
                        (int)prevCluster.size() >= fused_.maxDepth()) {                     // ... or we're beyond max depth
                        curClusters.push_back(prevCluster);                                 // Save this cluster
                        if (prevCluster.size() > 0)
                        {
                            prevCluster = prevClusters.back();
                            prevClusters.pop_back();
                        }
                    }
                    else {
                        prevCluster.setQids(foundTotQids);                                  // New version of our cluster (appended)
                        prevCluster.append_gates(clusterFound.get_gates());
                    }
                }                                                                           // Keep looking for clusters to add
                curClusters.push_back(prevCluster);                                         // Save the final cluster
            }                                                                               // Start all over with the next larger span
        }
        
        return curClusters;
    }

    /// generic application of a gate
    template <class Gate>
    void apply(Gate const& g)
    {
        std::vector<qubit_t> cs;
        GateWrapper gateApplied = GateWrapper(cs, g.qubit(), g.matrix());
        gatelist_.push_back(gateApplied);
        if (gatelist_.size() > 999) {
            flush();
        }

        int doFlush = fused_.shouldFlush(wfn_, cs, g.qubit());
    }

    /// generic application of a multiply controlled gate
    template <class Gate>
    void apply_controlled(std::vector<qubit_t> cs, Gate const& g)
    {
        std::vector<qubit_t> pcs = qubits(cs);
        GateWrapper gateApplied = GateWrapper(cs, g.qubit(), g.matrix());
        gatelist_.push_back(gateApplied);
        if (gatelist_.size() > 999) {
            flush();
        }
        
        int doFlush = fused_.shouldFlush(wfn_, cs, g.qubit());
    }

    /// generic application of a controlled gate
    template <class Gate>
    void apply_controlled(qubit_t c, Gate const& g)
    {
        std::vector<qubit_t> cs(1, c);
        apply_controlled(cs, g);
    }

    /// unoptimized application of a doubly controlled gate
    template <class Gate>
    void apply_controlled(qubit_t c1, qubit_t c2, Gate const& g)
    {
        std::vector<qubit_t> cs;
        cs.push_back(c1);
        cs.push_back(c2);
        apply_controlled(cs, g);
    }

    template <class A>
    bool subsytemwavefunction(std::vector<unsigned> const& qs, std::vector<T, A>& qubitswfn, double tolerance)
    {
        flush(); // we have to flush before we can extract the state
        return kernels::subsytemwavefunction(wfn_, qubits(qs), qubitswfn, tolerance);
    }


    // apply permutation of basis states to the wave function
    void permute_basis(std::vector<unsigned> const& qs, std::size_t table_size,
        std::size_t const* permutation_table, bool adjoint = false)
    {
        assert(table_size == 1ull << qs.size());
        flush();
        auto real_qs = qubits(qs);
        auto num_states = wfn_.size();
        auto psi_new = WavefunctionStorage(num_states);
        auto qmask = kernels::make_mask(real_qs);

        auto permute = [&real_qs, qmask, table_size, permutation_table](std::size_t state) {
            auto qstate = detail::get_register(real_qs, state);
            assert(qstate < table_size);
            return detail::set_register(real_qs, qmask, permutation_table[qstate], state);
        };

        if (!adjoint)
        {
            for (size_t i = 0; i < num_states; ++i)
                psi_new[permute(i)] = wfn_[i];
        }
        else
        {
            for (size_t i = 0; i < num_states; ++i)
                psi_new[i] = wfn_[permute(i)];
        }

        std::swap(wfn_, psi_new);
    }

    RngEngine& rng()
    {
        return rng_;
    }


    std::vector<qubit_t> qubits(std::vector<qubit_t> const& qs) const
    {
        std::vector<qubit_t> ps;
        for (auto q : qs)
            ps.push_back(qubit(q));
        return ps;
    }

    // Returns the list of logical ids currently allocated.
    std::vector<qubit_t> logicalQubits() const
    {
        std::vector<qubit_t> qs;
        for (qubit_t i = 0; i < qubitmap_.size(); i++)
        {
            if (qubitmap_[i] != invalid_qubit()) qs.push_back(i);
        }
    
        return qs;
    }

  private:
    unsigned num_qubits_;             // for convenience
    mutable WavefunctionStorage wfn_; // storing the wave function
    mutable std::vector<qubit_t> qubitmap_;   // mapping of logical to physical qubits
	int usage_;
    mutable std::vector<GateWrapper> gatelist_;

    // randomness support
    RngEngine rng_;
    Fused fused_;
};


/// print information about the wave function
template <class T>
std::ostream& operator<<(std::ostream& out, Wavefunction<T> const& wfn)
{
    wfn.flush();
    out << "Wave function for " << wfn.num_qubits() << " with " << wfn.data().size() << " elements "
        << " using " << sizeof(T) * wfn.data().size() << " bytes" << std::endl;
    if (wfn.num_qubits() <= 6)
        std::copy(wfn.data().begin(), wfn.data().end(), std::ostream_iterator<T>(out, "\n"));
    return out;
}
}
}
}
