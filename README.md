# data_extraction_homework_assignment

language used c#, in .NET Core environment

Two webscraping excercises, for specific web pages.

Collected data is accordingly in "WebScraper.Flysas/data" and "WebScraper.Norwegian/data".
"WebScraper.Lib" is mostly a possibility to be a proper utilities library.

•	Provide the exact number of page loads (requests) that would be needed to extract the data;

For the Norwegian assignment part the exact number requests is 54 (for all the 30 days, of oneway, direct flights) , Flysas at the moment is 3 requests

•	Could the number of requests be reduced and how? Provide the exact number.

In case of Norwegian, I have already assumed that there will be no surprise flights on Saturdays, so they can be omitted (so far there is none), then the data also shows that the tax for this sort of query is also always the same thus additionally 29 requests could be ommitted, making it 25 requests. However I am not quite sure how much of that data can be passed on thinking it will not be there.

In case of Flysas, it can not be loaded with less, however it's probably best to have a heavily controlled headless browser scrape it instead of doing it request by request, it seems that ajax/asp pages are more difficult to understand (atleast at the start), and replicate - most of the date is mocked from making previous requests, some of it (I assume) can be stored for future reuse though. activating a client side .js would be good though.
