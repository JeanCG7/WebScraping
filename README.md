# WebScraping
.Net Core Console Application that scrap Pelando's website (products in discount) and generate a .CSV file with product's name, price, comments quantity, temperature* and link

This applications is a basic sample of web scrap technique. The console application get the Html from a website and then perform searchs on it to find product's list.
In this code, there's also usage of Parallel class. Using a Parallel.For and using Parallel.Invoke to execute multiples independent methods at the same time give us so much performance.
