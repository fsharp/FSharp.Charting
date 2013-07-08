﻿(** 
# FSharp.Charting: Bar and Column Charts

*Summary:* This example shows how to create bar and column charts in F#. The example visualizes the size of the populations of the continents. 

This example looks at how to create bar and column charts from F#. The input data in this example is an F# list of tuples containing continent names and total populations. The article demonstrates how to display a bar/column chart with names of continents as labels and the population as the value. A sample bar chart is shown in Figure 1.

<div>
    <img src="images/IC523396.png" alt="Sample Bar Chart">
</div>

A bar or a column chart can be created using the Chart.Column and Chart.Bar functions. 

All functions are overloaded and can be called with various types of parameters. When called with a list 
containing just Y values, the chart automatically uses the sequence 1, 2, 3… for the X values. Alternatively, 
it is possible to provide a list containing both X and Y values as a tuple, which gives a way to draw 2D 
curves and scatter plots as well. Here are three examples:

*)

#load "../bin/FSharp.Charting.fsx"

open FSharp.Charting
open System

let countryData = 
    [ "Africa", 1033043; 
      "Asia", 4166741; 
      "Europe", 732759; 
      "South America", 588649; 
      "North America", 351659; 
      "Oceania", 35838  ]

Chart.Bar countryData

Chart.Column countryData


(**
When using F# Interactive, each of these examples needs to be evaluated separately. This way, F# Interactive 
invokes a handler that automatically shows the created chart.

The first example specifies the data source as a single list that contains two-element tuples. The first 
element of the tuple is the X value (category) and the second element is the population. 

The second example creates a Column chart instead of a Bar chart.

The third example below shows that you may also simply give a set of Y values, rather than (X,Y) value pairs.

*)

Chart.Bar [ 0 .. 10 ] 
