
function notifyUserOfConnectionProblem() {
    //alert('experiencing some problems with connections');
}

function notifyUserOfTryingToReconnect() {
    //alert('reconnect issues');
}

$(function () {

    $.connection.hub.connectionSlow(function () {
        notifyUserOfConnectionProblem(); 
    });

    $.connection.hub.reconnecting(function () {
        notifyUserOfTryingToReconnect(); 
    });

    // Reference the auto-generated proxy for the hub.
    var messageStatusHub = $.connection.messageStatusHub;

    //Callback
    messageStatusHub.client.addModel = function (model) {

        // Add the message to the page.
        $('#messages').append('<li class="line"><strong>' + htmlEncode(model.message) + '</strong> ' + '</li>');
        $('.console').animate({ scrollTop: $('.console').prop("scrollHeight") }, 10);
    };   

    
    // Start the connection.
    $.connection.hub.start().done(function () {      

    });

    //ToDo: disconnect when complete

    
});

// This optional function html-encodes messages for display in the page.
function htmlEncode(value) {
    var encodedValue = $('<div />').text(value).html();
    return encodedValue;
}
