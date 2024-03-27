// See https://aka.ms/new-console-template for more information

using Core;

var video = new BunnyVideoDrm(
    // insert the referer between the quotes below (address of your webpage)
    referer: "https://iframe.mediadelivery.net/5056fb0a-a739-416e-92af-acfa505e7b3a/playlist.drm?contextId=99959a4c-f523-4e30-ade5-710de730cad4&secret=1b0ccff3-d931-42e1-b7ce-656f347ec164",
    // paste your embed link
    embedUrl: "https://iframe.mediadelivery.net/embed/21030/5056fb0a-a739-416e-92af-acfa505e7b3a?autoplay=false&loop=true",
    // you can override file name, no extension
    name: "test2",
    // you can override download path
    path: @"./Temp");

await video.Download();