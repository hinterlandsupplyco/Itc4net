# Itc4net: interval tree clocks for .NET

Itc4net is a C# implementation of Interval Tree Clocks (ITC), a causality tracking mechanism. 

*Disclaimer: While this project is intended for production use, it has not yet been used in production. Towards this goal, there is an extensive set of unit tests, but it still requires real-world use and testing.*

### Overview

This project is a C#/.NET implementation of the ideas presented in the 2008 paper, [Interval Tree Clocks: A Logical Clock for Dynamic Systems](http://gsd.di.uminho.pt/members/cbm/ps/itc2008.pdf). An Interval Tree Clock (ITC) provides a means of causality tracking in a distributed system with a dynamic number of participants and offers a way to determine the partial ordering of events, i.e., a happened-before relation.

The term *causality* in distributed systems originates from the concept of causality in physics where "causal connections gives us the only ordering of events that all observers will agree on" ([The Speed of Light is NOT About Light](https://youtu.be/msVuCEs8Ydo?t=44s) | [PBS Digital Studios | Space Time](https://www.youtube.com/channel/UC7_gcs09iThXybpVgjHZ_7g)). In relativity, observers moving relative to each other may not agree on the elapsed time or distance between events. Similarly, in distributed systems, a causal history (or compressed representation) is necessary to determine the partial ordering of events or the detection of inconsistent data replicas because physical clocks are unreliable.

### Getting Started

To get started, the best way to become familiar with the basics of interval tree clocks is to read the [ITC paper](http://gsd.di.uminho.pt/members/cbm/ps/itc2008.pdf). The first 3 sections provide an overview and explain the fork-event-join model. Don't worry, it is <u>not</u> necessary to understand how the kernel operations are implemented.

Install using the NuGet Package Manager:

```
Install-Package Itc4net -Pre
```

#### Stamp

An ITC stamp is composed of two parts: an ID and event tree (i.e., casual past). The Stamp class default constructor creates a *seed* stamp. ITC stamps use a string notation with the following form: "(i,e)"

```c#
Stamp seed = new Stamp(); // (1,0) <-- that's id:1, event:0
```

Although it is not specified (or required) by the ITC paper, Itc4net uses immutable types for the core ITC Stamp, Id, and Event classes; therefore, the ITC kernel operations (fork, join, and event) are pure functions. As a consequence, it is important to treat the Stamp like a struct type and replace the reference with a new reference when performing any kernel operations. 

For example, do this:

```c#
Stamp s = new Stamp();	// (1,0)
s = s.Event();			// (1,1) inflated event
s = s.Event();			// (1,2) inflated event
```

Do **not** do this, as it will lead to incorrect results:

```c#
Stamp s = new Stamp();	// (1,0)
s.Event();				// (1,1) inflated event
s.Event();				// (1,1) oops, same as before!
```

#### Fork

The fork operation generates a pair of stamps with distinct IDs, each with a copy of the event tree. This operation is used to generate new stamps (after creating the initial seed stamp). More specifically, it splits the ID of the original stamp into two distinct IDs, so you will want to keep a reference to one stamp and share the other. In the next example, variable *s* would likely be a private member field stored in a class responsible for generating timestamps.

```c#
Stamp s = new Stamp();

Tuple<Stamp, Stamp> forked = s.Fork();
s = forked.Item1; // replace variable s with one of the generated stamps
Stamp another = forked.Item2; // a logical clock for use by another process
```

Since extracting multiple multiple items from tuples is awkwardly verbose, there are extension methods that provide an alternative API with out parameters, if preferred:

```c#
Stamp s = new Stamp();
Stamp another;
s.Fork(out s, out another);
```

Multiple stamps can be generated by successive calls to Fork. Although it may not be necessary to create multiple stamps at once, it is useful enough that there are extensions to generate 3 or 4 stamps at a time.

```c#
Stamp s = new Stamp();		// (1,0)
var forked = s.Fork4();
Stamp s1 = forked.Item1;    // (((1,0),0),0)
Stamp s2 = forked.Item2;    // (((0,1),0),0)
Stamp s3 = forked.Item3;    // ((0,(1,0)),0)
Stamp s4 = forked.Item4;    // ((0,(0,1)),0)

-- or alternatively --

Stamp s = new Stamp();
Stamp s1, s2, s3, s4;
s.Fork(out s1, out s2, out s3, out s4);
```

*A note about logical clock identifiers: the ID of a logical clock needs to be unique in the system. Some approaches use integers which works well when there is a global authority that can hand-out identities or when the system uses a fixed number of participants. Some approaches use UUIDs (or other globally unique naming strategy) which allows any number of participants, but tracking casual histories for each participant leads to very large timestamps. Instead, ITC uses the fork operation to generate a distinct pair of IDs from an existing stamp. This allows a dynamic number of participants and eliminates the need for a global authority, as any (non-anonymous) stamp can generate new IDs.*

### Additional Resources

- [Interval Tree Clocks: A Logical Clock for Dynamic Systems](http://gsd.di.uminho.pt/members/cbm/ps/itc2008.pdf)
- [Logical time resources](https://gist.github.com/macintux/a79a254dd0bdd330702b): A convenient collection of links to logical time whitepapers and resources, including Lamport timestamps, version vectors, vector clocks, and more.
- [The trouble with timestamps](https://aphyr.com/posts/299-the-trouble-with-timestamps)
- [Provenance and causality in distributed systems](http://blog.jessitron.com/2016/09/provenance-and-causality-in-distributed.html): An excellent description of causality, why it's important and useful; a call to action!

### Implementation Details

- The 2008 ITC paper provides exceptional and detailed descriptions of how to implement the kernel operations (fork-event-join model). This project implements the timestamps, IDs, and events directly from the descriptions and formulas in the ITC paper; it is not a port of any existing ITC implementations.
- The core ITC classes are immutable. Accordingly, the core ITC kernel operations: fork, event, and join are pure functions.
- The ID and event classes use a [discriminated union technique](http://stackoverflow.com/a/3199453) (or as close as you can get in C#) to distinguish between internal (node) and external (leaf) tree nodes. A *Match* function eliminates the need to cast and provides concise code without the need for type checks and casting.