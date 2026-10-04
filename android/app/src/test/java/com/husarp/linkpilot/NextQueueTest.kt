package com.husarp.linkpilot

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

// The smart queue's order (NextQueue in Store.kt): what Next does tap after tap.
class NextQueueTest {
    private val names = listOf("A", "B", "C", "D")

    @Test fun liveFirstThenRecentThenListOrder() =
        assertEquals(listOf("C", "A", "D", "B"), NextQueue.order(names, listOf("A", "D"), "C"))

    @Test fun oneTapFlipsBack() {
        // B was live, then A: Next goes back to B, and the next single tap back to A
        val first = NextQueue.order(names, listOf("A", "B"), "A")
        assertEquals("B", NextQueue.step(first, "A", 1))
        val recent = NextQueue.landed(first, "B")
        assertEquals("A", NextQueue.step(NextQueue.order(names, recent, "B"), "B", 1))
    }

    @Test fun aRunWalksOnThroughTheRest() {
        // taps in a row keep the order the run began with, so they do not flip between two
        val run = NextQueue.order(names, listOf("A", "B"), "A")
        assertEquals("B", NextQueue.step(run, "A", 1))
        assertEquals("C", NextQueue.step(run, "B", 1))
        assertEquals("D", NextQueue.step(run, "C", 1))
        assertEquals("A", NextQueue.step(run, "D", 1))
        // walking A, B, C leaves C, A, B
        assertEquals(listOf("C", "A", "B", "D"), NextQueue.landed(run, "C"))
    }

    @Test fun previousGoesTheOtherWay() =
        assertEquals("D", NextQueue.step(NextQueue.order(names, listOf("A", "B"), "A"), "A", -1))

    @Test fun liveNotInTheList() {
        assertEquals("A", NextQueue.step(names, "X", 1))
        assertEquals("D", NextQueue.step(names, "X", -1))
    }

    @Test fun nothingToStepThrough() = assertNull(NextQueue.step(emptyList(), "A", 1))
}
