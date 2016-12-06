﻿using System;
using System.Collections;
using System.Diagnostics.Contracts;
using System.IO;
using Itc4net.Binary;
using Itc4net.Text;

namespace Itc4net
{
    /// <summary>
    /// An ITC stamp (logical clock)
    /// </summary>
    public class Stamp : IEquatable<Stamp>
    {
        readonly Id _i;
        readonly Event _e;

        /// <summary>
        /// Initializes a new seed instance of the <see cref="Stamp"/> class.
        /// </summary>
        public Stamp() // seed
        {
            _i = 1;
            _e = new Event.Leaf(0);
        }

        public Stamp(Id i, Event e)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (e == null) throw new ArgumentNullException(nameof(e));

            _i = i;
            _e = e;
        }

        public bool IsAnonymous => _i == 0;

        /// <summary>
        /// Happened-before relation
        /// </summary>
        public bool Leq(Stamp other)
        {
            return _e.Leq(other._e);
        }

        /// <summary>
        /// Happened-before relation
        /// </summary>
        //public static bool Leq(Stamp s1, Stamp s2)
        //{
        //    return s1._e.Leq(s2._e);
        //}

        /// <summary>
        /// Fork a stamp into 2 distinct stamps (returns a 2-tuple)
        /// </summary>
        /// <returns>
        /// A 2-tuple, each with a unique identity and a copy of the causal history.
        /// </returns>
        [Pure]
        public Tuple<Stamp, Stamp> Fork()
        {
            var i = _i.Split();
            return Tuple.Create(
                new Stamp(i.L, _e),
                new Stamp(i.R, _e)
            );
        }

        /// <summary>
        /// Create an anonymous stamp with a copy of the causal history.
        /// </summary>
        [Pure]
        public Stamp Peek()
        {
            return new Stamp(0, _e);
        }

        /// <summary>
        /// Inflate (increment) the stamp.
        /// </summary>
        /// <remarks>
        /// In the ITC paper, this kernel operation is named "event".
        /// </remarks>
        [Pure]
        public Stamp Event()
        {
            Event fillEvent = Fill();
            if (_e != fillEvent)
            {
                return new Stamp(_i, fillEvent);
            }

            return new Stamp(_i, Grow().Item1);
        }

        /// <summary>
        /// Merge stamps.
        /// </summary>
        [Pure]
        public Stamp Join(Stamp other)
        {
            Id i = _i.Sum(other._i);
            Event e = _e.Join(other._e);

            return new Stamp(i, e);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"({_i},{_e})";
        }

        public bool Equals(Stamp other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _i.Equals(other._i) && _e.Equals(other._e);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var other = obj as Stamp;
            return other != null && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (_i.GetHashCode()*397) ^ _e.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public static bool operator ==(Stamp left, Stamp right)
        {
            return Equals(left, right);
        }

        /// <inheritdoc/>
        public static bool operator !=(Stamp left, Stamp right)
        {
            return !Equals(left, right);
        }

        internal Tuple<Event, int> Grow()
        {
            Tuple<Event, int> error = Tuple.Create((Event)null, -1);

            var result = _e.Match(n =>
                {
                    if (_i == 1)
                    {
                        return Tuple.Create((Event)(n + 1), 0);
                    }

                    var g = new Stamp(_i, new Event.Node(n, 0, 0)).Grow();
                    Event eʹ = g.Item1;
                    int c = g.Item2;
                    return Tuple.Create(eʹ, c + 1000);
                },
                (n, el, er) =>
                {
                    return _i.Match(
                        v => error,
                        (il, ir) =>
                        {
                            if (il == 0)
                            {
                                var g = new Stamp(ir, er).Grow();
                                var eʹr = g.Item1;
                                var cr = g.Item2;
                                return Tuple.Create((Event)new Event.Node(n, el, eʹr), cr + 1);
                            }

                            if (ir == 0)
                            {
                                var g = new Stamp(il, el).Grow();
                                var eʹl = g.Item1;
                                var cl = g.Item2;
                                return Tuple.Create((Event)new Event.Node(n, eʹl, er), cl + 1);
                            }

                            {
                                var gl = new Stamp(il, el).Grow();
                                var eʹl = gl.Item1;
                                var cl = gl.Item2;

                                var gr = new Stamp(ir, er).Grow();
                                var eʹr = gr.Item1;
                                var cr = gr.Item2;

                                if (cl < cr)
                                {
                                    return Tuple.Create((Event)new Event.Node(n, eʹl, er), cl + 1);
                                }
                                else // cl >= cr
                                {
                                    return Tuple.Create((Event)new Event.Node(n, el, eʹr), cr + 1);
                                }
                            }
                        });
                });

            if (result.Equals(error))
            {
                throw new InvalidOperationException($"Failed to grow stamp {this}");
            }

            return result;
        }

        internal Event Fill()
        {
            Event unchanged = _e;

            return _i.Match(
                v =>
                {
                    switch (v)
                    {
                        case 0:
                            return _e;
                        case 1:
                            return Itc4net.Event.Create(_e.Max());
                        default:
                            throw new InvalidOperationException();
                    }
                },
                (il, ir) =>
                {
                    if (il == 1)
                    {
                        return _e.Match(n => unchanged, (n, el, er) =>
                        {
                            var eʹr = new Stamp(ir, er).Fill();
                            Event e = Itc4net.Event.Create(n, Itc4net.Event.Create(Math.Max(el.Max(), eʹr.Min())), eʹr);
                            return e.Normalize();
                        });
                    }

                    if (ir == 1)
                    {
                        return _e.Match(n => unchanged, (n, el, er) =>
                        {
                            var eʹl = new Stamp(il, el).Fill();
                            Event e = Itc4net.Event.Create(n, eʹl, Itc4net.Event.Create(Math.Max(er.Max(), eʹl.Min())));
                            return e.Normalize();
                        });
                    }

                    return _e.Match(n => unchanged, (n, el, er) =>
                    {
                        var eʹl = new Stamp(il, el).Fill();
                        var eʹr = new Stamp(ir, er).Fill();
                        Event e = Itc4net.Event.Create(n, eʹl, eʹr);
                        return e.Normalize();
                    });
                }
            );
        }

        public static Stamp Parse(string text)
        {
            var parser = new Parser();
            return parser.ParseStamp(text);
        }

        public byte[] ToBinary()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BitWriter(stream, leaveOpen: true))
                {
                    _i.WriteTo(writer);
                    _e.WriteTo(writer);
                }

                return stream.ToArray();
            }
        }
    }
}